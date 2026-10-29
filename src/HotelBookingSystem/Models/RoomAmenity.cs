using System.ComponentModel.DataAnnotations;

namespace HotelBookingSystem.Models;

public class RoomAmenity
{
    // [Key] is needed because EF only auto-detects "Id" or "RoomAmenityId"
    [Key]
    public int AmenityId { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<RoomAmenityMapping> RoomMappings { get; set; } = new List<RoomAmenityMapping>();
}