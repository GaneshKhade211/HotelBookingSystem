using HotelBookingSystem.Data;
using HotelBookingSystem.Extensions;
using HotelBookingSystem.Models;
using HotelBookingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSystem.Services;

public class RoomService : IRoomService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;

    public RoomService(ApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    // ---------------- Queries ----------------
    public async Task<List<RoomListItemViewModel>> GetAllRoomsAsync() =>
        await _db.Rooms
            .AsNoTracking()
            .OrderBy(r => r.RoomNumber)
            .Select(r => new RoomListItemViewModel
            {
                RoomId = r.RoomId,
                RoomNumber = r.RoomNumber,
                RoomTypeName = r.RoomType.Name,
                Capacity = r.Capacity,
                PricePerNight = r.PricePerNight,
                IsActive = r.IsActive,
                AmenityCount = r.AmenityMappings.Count
            })
            .ToListAsync();

    public async Task<RoomDetailsViewModel?> GetRoomDetailsAsync(int roomId)
    {
        var today = _time.GetHotelToday();

        return await _db.Rooms
            .AsNoTracking()
            .Where(r => r.RoomId == roomId)
            .Select(r => new RoomDetailsViewModel
            {
                RoomId = r.RoomId,
                RoomNumber = r.RoomNumber,
                RoomTypeName = r.RoomType.Name,
                RoomTypeDescription = r.RoomType.Description,
                Description = r.Description,
                Capacity = r.Capacity,
                PricePerNight = r.PricePerNight,
                IsActive = r.IsActive,
                ImageUrl = r.ImageUrl,
                Amenities = r.AmenityMappings.Select(m => m.Amenity.Name).OrderBy(n => n).ToList(),
                UpcomingBookings = r.Bookings.Count(b =>
                    (b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Confirmed)
                    && b.CheckOutDate > today)
            })
            .SingleOrDefaultAsync();
    }

    public async Task<RoomFormViewModel?> GetRoomForEditAsync(int roomId)
    {
        var room = await _db.Rooms
            .AsNoTracking()
            .Include(r => r.AmenityMappings)
            .SingleOrDefaultAsync(r => r.RoomId == roomId);

        if (room is null)
        {
            return null;
        }

        var model = new RoomFormViewModel
        {
            RoomId = room.RoomId,
            RoomNumber = room.RoomNumber,
            RoomTypeId = room.RoomTypeId,
            Description = room.Description,
            Capacity = room.Capacity,
            PricePerNight = room.PricePerNight,
            IsActive = room.IsActive,
            ImageUrl = room.ImageUrl,
            SelectedAmenityIds = room.AmenityMappings.Select(m => m.AmenityId).ToList()
        };

        await PopulateFormOptionsAsync(model);
        return model;
    }

    public async Task PopulateFormOptionsAsync(RoomFormViewModel model)
    {
        model.RoomTypeOptions = await _db.RoomTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem(t.Name, t.RoomTypeId.ToString()))
            .ToListAsync();

        model.AmenityOptions = await _db.RoomAmenities
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AmenityOption(a.AmenityId, a.Name))
            .ToListAsync();
    }

    // ---------------- Commands ----------------
    public async Task<ServiceResult> CreateRoomAsync(RoomFormViewModel model)
    {
        var validation = await ValidateAsync(model, existingRoomId: null);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var room = new Room
        {
            RoomNumber = NormalizeRoomNumber(model.RoomNumber),
            RoomTypeId = model.RoomTypeId,
            Description = CleanText(model.Description),
            Capacity = model.Capacity,
            PricePerNight = model.PricePerNight,
            IsActive = model.IsActive,
            ImageUrl = CleanText(model.ImageUrl),
            AmenityMappings = model.SelectedAmenityIds
                .Distinct()
                .Select(id => new RoomAmenityMapping { AmenityId = id })
                .ToList()
        };

        _db.Rooms.Add(room);
        return await SaveAsync(room.RoomNumber, () => ServiceResult.Success(room.RoomId));
    }

    public async Task<ServiceResult> UpdateRoomAsync(RoomFormViewModel model)
    {
        var room = await _db.Rooms
            .Include(r => r.AmenityMappings)
            .SingleOrDefaultAsync(r => r.RoomId == model.RoomId);

        if (room is null)
        {
            return ServiceResult.Fail("Room not found.");
        }

        var validation = await ValidateAsync(model, existingRoomId: room.RoomId);
        if (!validation.Succeeded)
        {
            return validation;
        }

        // Rule: capacity cannot drop below the guest count of any upcoming active booking
        var today = _time.GetHotelToday();
        var largestUpcomingGroup = await _db.Bookings
            .Where(b => b.RoomId == room.RoomId
                        && (b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Confirmed)
                        && b.CheckOutDate > today)
            .MaxAsync(b => (int?)b.NumberOfGuests) ?? 0;

        if (model.Capacity < largestUpcomingGroup)
        {
            return ServiceResult.Fail(
                $"Capacity cannot be lower than {largestUpcomingGroup}: an upcoming booking has that many guests.",
                nameof(RoomFormViewModel.Capacity));
        }

        room.RoomNumber = NormalizeRoomNumber(model.RoomNumber);
        room.RoomTypeId = model.RoomTypeId;
        room.Description = CleanText(model.Description);
        room.Capacity = model.Capacity;
        room.PricePerNight = model.PricePerNight;  // existing bookings keep their own price snapshot
        room.IsActive = model.IsActive;
        room.ImageUrl = CleanText(model.ImageUrl);

        // Sync amenities: remove unticked, add newly ticked
        var wanted = model.SelectedAmenityIds.ToHashSet();
        foreach (var mapping in room.AmenityMappings.Where(m => !wanted.Contains(m.AmenityId)).ToList())
        {
            room.AmenityMappings.Remove(mapping);
        }
        var current = room.AmenityMappings.Select(m => m.AmenityId).ToHashSet();
        foreach (var amenityId in wanted.Where(id => !current.Contains(id)))
        {
            room.AmenityMappings.Add(new RoomAmenityMapping { AmenityId = amenityId });
        }

        return await SaveAsync(room.RoomNumber, () => ServiceResult.Success(room.RoomId));
    }

    // Deactivating blocks NEW bookings only. Existing bookings are not cancelled automatically.
    public async Task<ServiceResult> SetActiveAsync(int roomId, bool isActive)
    {
        var room = await _db.Rooms.FindAsync(roomId);
        if (room is null)
        {
            return ServiceResult.Fail("Room not found.");
        }

        room.IsActive = isActive;
        await _db.SaveChangesAsync();
        return ServiceResult.Success(room.RoomId);
    }

    // ---------------- Helpers ----------------
    private async Task<ServiceResult> ValidateAsync(RoomFormViewModel model, int? existingRoomId)
    {
        var roomNumber = NormalizeRoomNumber(model.RoomNumber);
        if (await _db.Rooms.AnyAsync(r => r.RoomNumber == roomNumber
                                          && (existingRoomId == null || r.RoomId != existingRoomId)))
        {
            return ServiceResult.Fail($"Room number {roomNumber} already exists.", nameof(RoomFormViewModel.RoomNumber));
        }

        if (!await _db.RoomTypes.AnyAsync(t => t.RoomTypeId == model.RoomTypeId))
        {
            return ServiceResult.Fail("The selected room type does not exist.", nameof(RoomFormViewModel.RoomTypeId));
        }

        var amenityIds = model.SelectedAmenityIds.Distinct().ToList();
        if (amenityIds.Count > 0
            && await _db.RoomAmenities.CountAsync(a => amenityIds.Contains(a.AmenityId)) != amenityIds.Count)
        {
            return ServiceResult.Fail("One or more selected amenities do not exist.", nameof(RoomFormViewModel.SelectedAmenityIds));
        }

        if (!IsSafeImageUrl(model.ImageUrl))
        {
            return ServiceResult.Fail(
                "Image URL must start with https:// or be a site path starting with /images/.",
                nameof(RoomFormViewModel.ImageUrl));
        }

        return ServiceResult.Success();
    }

    // Saves, and turns a unique-index violation (two admins saving the same number at once)
    // into a friendly error instead of a crash.
    private async Task<ServiceResult> SaveAsync(string roomNumber, Func<ServiceResult> onSuccess)
    {
        try
        {
            await _db.SaveChangesAsync();
            return onSuccess();
        }
        catch (DbUpdateException)
        {
            if (await _db.Rooms.AsNoTracking().AnyAsync(r => r.RoomNumber == roomNumber))
            {
                return ServiceResult.Fail($"Room number {roomNumber} already exists.", nameof(RoomFormViewModel.RoomNumber));
            }
            throw;
        }
    }

    private static string NormalizeRoomNumber(string roomNumber) => roomNumber.Trim().ToUpperInvariant();

    private static string? CleanText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Blocks javascript:, data: and http: URLs, and path tricks like "/images/../"
    private static bool IsSafeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        url = url.Trim();

        if (url.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) && !url.Contains(".."))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    // ---------------- Public browsing and search (Phase 8) ----------------
    public async Task<List<RoomCardViewModel>> GetActiveRoomCardsAsync() =>
        await ToCards(_db.Rooms.AsNoTracking().Where(r => r.IsActive), nights: 0)
            .OrderBy(c => c.PricePerNight)
            .ToListAsync();

    public async Task<List<RoomCardViewModel>> SearchAvailableRoomsAsync(DateOnly checkIn, DateOnly checkOut, int guests)
    {
        var blocksStay = BookingRules.BlocksStay(checkIn, checkOut);
        var nights = BookingRules.CountNights(checkIn, checkOut);

        // Active rooms, big enough, with NO blocking booking that overlaps the requested dates.
        // Runs as ONE SQL query (NOT EXISTS subquery) on the server.
        var rooms = _db.Rooms
            .AsNoTracking()
            .Where(r => r.IsActive && r.Capacity >= guests)
            .Where(r => !r.Bookings.AsQueryable().Any(blocksStay));

        return await ToCards(rooms, nights)
            .OrderBy(c => c.PricePerNight)
            .ToListAsync();
    }

    public async Task<bool> IsRoomAvailableAsync(int roomId, DateOnly checkIn, DateOnly checkOut) =>
        !await _db.Bookings
            .Where(b => b.RoomId == roomId)
            .AnyAsync(BookingRules.BlocksStay(checkIn, checkOut));

    private static IQueryable<RoomCardViewModel> ToCards(IQueryable<Room> rooms, int nights) =>
        rooms.Select(r => new RoomCardViewModel
        {
            RoomId = r.RoomId,
            RoomNumber = r.RoomNumber,
            RoomTypeName = r.RoomType.Name,
            Description = r.Description,
            Capacity = r.Capacity,
            PricePerNight = r.PricePerNight,
            ImageUrl = r.ImageUrl,
            Amenities = r.AmenityMappings.Select(m => m.Amenity.Name).OrderBy(n => n).ToList(),
            Nights = nights,
            TotalPrice = r.PricePerNight * nights
        });
}