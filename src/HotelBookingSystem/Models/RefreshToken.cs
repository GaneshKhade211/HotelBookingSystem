using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBookingSystem.Models;

public class RefreshToken
{
    public int RefreshTokenId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    // Only the SHA-256 hash (64 hex characters) is stored, never the raw token
    [Required, StringLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }

    // Points to the token that replaced this one during rotation (reuse detection)
    public int? ReplacedByTokenId { get; set; }
    public RefreshToken? ReplacedByToken { get; set; }

    // Helper properties, not stored in the database
    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    [NotMapped]
    public bool IsActive => RevokedAtUtc == null && !IsExpired;
}