namespace HotelBookingSystem.Services;

// The raw tokens handed to the browser (as HttpOnly cookies). Never stored raw in the database.
public sealed record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);