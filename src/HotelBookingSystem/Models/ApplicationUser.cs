using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace HotelBookingSystem.Models;

// Extends Identity's built-in user (Id, Email, NormalizedEmail, PasswordHash, ...)
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    // Lets an admin block a user without deleting their booking history
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}