namespace HotelBookingSystem.Models;

// Many-to-many link between Rooms and RoomAmenities.
// Composite primary key (RoomId, AmenityId) is configured in ApplicationDbContext.
public class RoomAmenityMapping
{
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public int AmenityId { get; set; }
    public RoomAmenity Amenity { get; set; } = null!;
}