using System.ComponentModel.DataAnnotations;

namespace HotelBookingSystem.Models;

public class Booking
{
    public int BookingId { get; set; }

    [Required, StringLength(20)]
    public string BookingReference { get; set; } = string.Empty;

    // Always taken from the logged-in user's claims, never from the browser
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    // DateOnly maps to SQL 'date': hotel stays are whole days, no times
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }

    [Range(1, 10)]
    public int NumberOfGuests { get; set; }

    [Range(1, 30)]
    public int NumberOfNights { get; set; }

    // Snapshot of the room price at booking time. Calculated on the server.
    public decimal PricePerNight { get; set; }
    public decimal TotalAmount { get; set; }

    public BookingStatus BookingStatus { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // SQL Server 'rowversion': stops two admins overwriting each other's changes
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}