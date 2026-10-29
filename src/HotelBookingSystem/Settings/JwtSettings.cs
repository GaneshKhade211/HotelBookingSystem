using System.ComponentModel.DataAnnotations;

namespace HotelBookingSystem.Settings;

// Bound from the "Jwt" section of configuration (appsettings.json + User Secrets)
public class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    // Base64 signing key from User Secrets / environment variables.
    // MinLength 44 = at least 32 bytes (256 bits), the minimum for HMAC-SHA256.
    [Required, MinLength(44)]
    public string Key { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 30)]
    public int RefreshTokenDays { get; set; } = 7;
}