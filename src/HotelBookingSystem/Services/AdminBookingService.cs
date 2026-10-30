using System.Linq.Expressions;
using HotelBookingSystem.Data;
using HotelBookingSystem.Extensions;
using HotelBookingSystem.Models;
using HotelBookingSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSystem.Services;

public class AdminBookingService : IAdminBookingService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<AdminBookingService> _logger;

    public AdminBookingService(ApplicationDbContext db, TimeProvider time, ILogger<AdminBookingService> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    // ---------------- Dashboard statistics ----------------
    public async Task<AdminDashboardViewModel> GetDashboardAsync()
    {
        var today = _time.GetHotelToday();

        var model = new AdminDashboardViewModel
        {
            Today = today,
            TotalUsers = await _db.Users.CountAsync(),
            TotalRooms = await _db.Rooms.CountAsync(),
            ActiveRooms = await _db.Rooms.CountAsync(r => r.IsActive),
            TotalBookings = await _db.Bookings.CountAsync()
        };

        // Bookings by status (one GROUP BY query)
        var byStatus = await _db.Bookings
            .GroupBy(b => b.BookingStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        model.PendingBookings = byStatus.GetValueOrDefault(BookingStatus.Pending);
        model.ConfirmedBookings = byStatus.GetValueOrDefault(BookingStatus.Confirmed);
        model.CancelledBookings = byStatus.GetValueOrDefault(BookingStatus.Cancelled);
        model.CompletedBookings = byStatus.GetValueOrDefault(BookingStatus.Completed);

        // Occupancy tonight: rooms with an active booking that covers tonight
        // (check-in on or before today AND check-out after today)
        model.OccupiedTonight = await _db.Bookings
            .Where(b => (b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Confirmed)
                        && b.CheckInDate <= today && b.CheckOutDate > today)
            .Select(b => b.RoomId)
            .Distinct()
            .CountAsync();

        model.OccupancyPercent = model.ActiveRooms == 0
            ? 0
            : Math.Round(100m * model.OccupiedTonight / model.ActiveRooms, 1);

        model.CheckInsToday = await _db.Bookings.CountAsync(b =>
            (b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Confirmed)
            && b.CheckInDate == today);

        model.CheckOutsToday = await _db.Bookings.CountAsync(b =>
            b.BookingStatus != BookingStatus.Cancelled && b.CheckOutDate == today);

        model.BookedValue = await _db.Bookings
            .Where(b => b.BookingStatus == BookingStatus.Confirmed || b.BookingStatus == BookingStatus.Completed)
            .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

        // Next 7 days of arrivals
        var nextWeek = today.AddDays(7);
        model.UpcomingArrivals = await _db.Bookings
            .AsNoTracking()
            .Where(b => (b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Confirmed)
                        && b.CheckInDate >= today && b.CheckInDate <= nextWeek)
            .OrderBy(b => b.CheckInDate)
            .Take(10)
            .Select(ToListItem)
            .ToListAsync();

        return model;
    }

    // ---------------- All bookings (filter + search) ----------------
    public async Task<List<AdminBookingListItemViewModel>> GetBookingsAsync(AdminBookingFilterViewModel filter)
    {
        var query = _db.Bookings.AsNoTracking().AsQueryable();

        if (filter.Status is { } status)
        {
            query = query.Where(b => b.BookingStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // EF turns Contains() into a parameterized LIKE: safe from SQL injection
            var term = filter.Search.Trim();
            query = query.Where(b =>
                b.BookingReference.Contains(term)
                || b.User.FullName.Contains(term)
                || (b.User.Email != null && b.User.Email.Contains(term))
                || b.Room.RoomNumber == term);
        }

        return await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(AdminBookingFilterViewModel.MaxRows)
            .Select(ToListItem)
            .ToListAsync();
    }

    // ---------------- One booking ----------------
    public async Task<AdminBookingDetailsViewModel?> GetBookingAsync(int bookingId)
    {
        var booking = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.BookingId == bookingId)
            .Select(b => new AdminBookingDetailsViewModel
            {
                BookingId = b.BookingId,
                BookingReference = b.BookingReference,
                GuestName = b.User.FullName,
                GuestEmail = b.User.Email ?? string.Empty,
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
                UpdatedAtUtc = b.UpdatedAtUtc,
                RowVersion = b.RowVersion
            })
            .SingleOrDefaultAsync();

        if (booking is not null)
        {
            booking.AllowedTransitions = BookingRules
                .AllowedAdminTransitions(booking.BookingStatus, booking.CheckInDate, _time.GetHotelToday())
                .ToList();
        }

        return booking;
    }

    // ---------------- Status change (rules + optimistic concurrency) ----------------
    public async Task<ServiceResult> UpdateStatusAsync(int bookingId, BookingStatus newStatus, byte[] rowVersion)
    {
        var booking = await _db.Bookings.SingleOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
        {
            return ServiceResult.Fail("Booking not found.");
        }

        // 1. Business rule, checked against the CURRENT database state
        var allowed = BookingRules.AllowedAdminTransitions(booking.BookingStatus, booking.CheckInDate, _time.GetHotelToday());
        if (!allowed.Contains(newStatus))
        {
            return ServiceResult.Fail($"A {booking.BookingStatus} booking cannot be changed to {newStatus}.");
        }

        // 2. Optimistic concurrency: tell EF which version the admin was looking at.
        //    The UPDATE only succeeds if the row still has that RowVersion.
        _db.Entry(booking).Property(b => b.RowVersion).OriginalValue = rowVersion;

        var oldStatus = booking.BookingStatus;
        booking.BookingStatus = newStatus;
        booking.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail(
                "This booking was changed by someone else while you were viewing it. The latest version is shown; please review and try again.");
        }

        _logger.LogInformation("Admin changed booking {Reference} from {Old} to {New}",
            booking.BookingReference, oldStatus, newStatus);
        return ServiceResult.Success(booking.BookingId);
    }

    // Shared list projection (translated to SQL)
    private static readonly Expression<Func<Booking, AdminBookingListItemViewModel>> ToListItem =
        b => new AdminBookingListItemViewModel
        {
            BookingId = b.BookingId,
            BookingReference = b.BookingReference,
            GuestName = b.User.FullName,
            GuestEmail = b.User.Email ?? string.Empty,
            RoomNumber = b.Room.RoomNumber,
            CheckInDate = b.CheckInDate,
            CheckOutDate = b.CheckOutDate,
            NumberOfNights = b.NumberOfNights,
            NumberOfGuests = b.NumberOfGuests,
            TotalAmount = b.TotalAmount,
            BookingStatus = b.BookingStatus,
            CreatedAtUtc = b.CreatedAtUtc
        };
}