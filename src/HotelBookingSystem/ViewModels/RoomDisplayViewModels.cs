namespace HotelBookingSystem.ViewModels;

// One row in the admin room list
public class RoomListItemViewModel
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal PricePerNight { get; set; }
    public bool IsActive { get; set; }
    public int AmenityCount { get; set; }
}

// Full details of one room (reused for the public details page in Phase 8)
public class RoomDetailsViewModel
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public string? RoomTypeDescription { get; set; }
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public decimal PricePerNight { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Amenities { get; set; } = new();
    public int UpcomingBookings { get; set; }
}