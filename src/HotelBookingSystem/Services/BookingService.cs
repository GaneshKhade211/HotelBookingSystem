using System.Security.Cryptography;
using HotelBookingSystem.Data;
using HotelBookingSystem.Extensions;
using HotelBookingSystem.Models;
using HotelBookingSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSystem.Services;

public class BookingService : IBookingService
{
    // Unambiguous characters only (no 0/O, 1/I) so references are easy to read out
    private const string ReferenceChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<BookingService> _logger;

    public BookingService(ApplicationDbContext db, TimeProvider time, ILogger<BookingService> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    // ---------------- Quote (display only) ----------------
    public async Task<BookingQuoteViewModel?> GetQuoteAsync(int roomId, DateOnly checkIn, DateOnly checkOut)
    {
        var nights = BookingRules.CountNights(checkIn, checkOut);

        var quote = await _db.Rooms
            .AsNoTracking()
            .Where(r => r.RoomId == roomId && r.IsActive)
            .Select(r => new BookingQuoteViewModel
            {
                RoomId = r.RoomId,
                RoomNumber = r.RoomNumber,
                RoomTypeName = r.RoomType.Name,
                Capacity = r.Capacity,
                PricePerNight = r.PricePerNight,
                ImageUrl = r.ImageUrl,
                Nights = nights,
                TotalAmount = r.PricePerNight * nights
            })
            .SingleOrDefaultAsync();

        if (quote is not null)
        {
            quote.IsAvailable = !await _db.Bookings
                .Where(b => b.RoomId == roomId)
                .AnyAsync(BookingRules.BlocksStay(checkIn, checkOut));
        }

        return quote;
    }

    // ---------------- Create (concurrency-safe) ----------------
    public async Task<ServiceResult> CreateBookingAsync(
        string userId, int roomId, DateOnly checkIn, DateOnly checkOut, int guests)
    {
        // 1. Re-validate everything on the server. Never trust earlier pages.
        var today = _time.GetHotelToday();
        var errors = BookingRules.ValidateStay(checkIn, checkOut, guests, today);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors[0].Message, errors[0].Field);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        // 2. Serialize bookings for THIS room. A parallel request for the same room waits here
        //    until we commit or roll back. Other rooms are not affected.
        if (!await AcquireRoomLockAsync(roomId))
        {
            return ServiceResult.Fail("The room is busy right now. Please try again in a moment.");
        }

        // 3. Load the room inside the lock: it must exist, be active and be big enough
        var room = await _db.Rooms.SingleOrDefaultAsync(r => r.RoomId == roomId);
        if (room is null || !room.IsActive)
        {
            return ServiceResult.Fail("This room is not available for booking.");
        }

        if (guests > room.Capacity)
        {
            return ServiceResult.Fail($"This room fits up to {room.Capacity} guest(s).", "Guests");
        }

        // 4. The booking must belong to an existing, active user
        if (!await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive))
        {
            return ServiceResult.Fail("Your account cannot make bookings.");
        }

        // 5. Re-check availability INSIDE the lock (the check that actually prevents double booking)
        var isTaken = await _db.Bookings
            .Where(b => b.RoomId == roomId)
            .AnyAsync(BookingRules.BlocksStay(checkIn, checkOut));
        if (isTaken)
        {
            return ServiceResult.Fail(
                "Sorry, this room has just been booked for some of these dates. Please choose other dates.");
        }

        // 6. Price is calculated here from the database, never taken from the browser
        var nights = BookingRules.CountNights(checkIn, checkOut);
        var booking = new Booking
        {
            BookingReference = await GenerateReferenceAsync(today),
            UserId = userId,
            RoomId = room.RoomId,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = guests,
            NumberOfNights = nights,
            PricePerNight = room.PricePerNight,
            TotalAmount = room.PricePerNight * nights,
            BookingStatus = BookingStatus.Pending,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync(); // releases the room lock

        _logger.LogInformation("Booking {Reference} created for room {RoomId}", booking.BookingReference, roomId);
        return ServiceResult.Success(booking.BookingId);
        // Any early "return" above disposes the transaction without committing -> rollback -> lock released
    }

    // ---------------- Read (with ownership check) ----------------
    public async Task<BookingDetailsViewModel?> GetBookingForUserAsync(int bookingId, string userId)
    {
        var today = _time.GetHotelToday();

        return await _db.Bookings
            .AsNoTracking()
            .Where(b => b.BookingId == bookingId && b.UserId == userId) // ownership: other users' bookings are invisible
            .Select(b => new BookingDetailsViewModel
            {
                BookingId = b.BookingId,
                BookingReference = b.BookingReference,
                RoomNumber = b.Room.RoomNumber,
                RoomTypeName = b.Room.RoomType.Name,
                CheckInDate = b.CheckInDate,
                CheckOutDate = b.CheckOutDate,
                NumberOfGuests = b.NumberOfGuests,
                NumberOfNights = b.NumberOfNights,
                PricePerNight = b.PricePerNight,
                TotalAmount = b.TotalAmount,
                BookingStatus = b.BookingStatus,
                CreatedAtUtc = b.CreatedAtUtc,
                CanCancel = (b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Confirmed)
                            && b.CheckInDate > today
            })
            .SingleOrDefaultAsync();
    }

    // ---------------- Helpers ----------------
    // SQL Server application lock owned by the current transaction.
    // Returns false if it could not be obtained within 10 seconds.
    private async Task<bool> AcquireRoomLockAsync(int roomId)
    {
        var resource = $"booking-room-{roomId}";

        var result = await _db.Database.SqlQuery<int>($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = {resource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            SELECT @result AS [Value];
            """).ToListAsync();

        return result.Count == 1 && result[0] >= 0; // 0 = granted, 1 = granted after waiting
    }

    private async Task<string> GenerateReferenceAsync(DateOnly today)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = new string(RandomNumberGenerator.GetItems<char>(ReferenceChars.AsSpan(), 6));
            var reference = $"HB-{today:yyMMdd}-{code}";   // e.g. HB-261030-K7QM2D

            if (!await _db.Bookings.AnyAsync(b => b.BookingReference == reference))
            {
                return reference;
            }
        }

        throw new InvalidOperationException("Could not generate a unique booking reference.");
    }
}