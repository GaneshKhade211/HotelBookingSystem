using System.ComponentModel.DataAnnotations;

namespace HotelBookingSystem.Models;

// SIMULATED payments only. No card numbers, CVVs or other card data are ever stored.
public class Payment
{
    public int PaymentId { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public decimal Amount { get; set; }

    [Required, StringLength(30)]
    public string PaymentMethod { get; set; } = "Simulated";

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    [Required, StringLength(50)]
    public string TransactionReference { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}