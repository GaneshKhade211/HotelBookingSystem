using System.ComponentModel.DataAnnotations;

namespace HotelBookingSystem.Models;

public class Room
{
    public int RoomId { get; set; }

    [Required, StringLength(10)]
    public string RoomNumber { get; set; } = string.Empty;

    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(1, 10)]
    public int Capacity { get; set; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal PricePerNight { get; set; }

    // Rooms are deactivated, never deleted, so old bookings stay valid
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public ICollection<RoomAmenityMapping> AmenityMappings { get; set; } = new List<RoomAmenityMapping>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}