using System.ComponentModel.DataAnnotations;

namespace HotelBookingSystem.Models;

public class RoomType
{
    public int RoomTypeId { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
    
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}