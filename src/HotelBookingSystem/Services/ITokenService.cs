using HotelBookingSystem.Models;

namespace HotelBookingSystem.Services;

public interface ITokenService
{
    // Login: create a new access token + refresh token pair
    Task<AuthTokens> IssueTokensAsync(ApplicationUser user);

    // Refresh: returns null if the refresh token is invalid, expired, revoked or reused
    Task<AuthTokens?> RotateRefreshTokenAsync(string refreshToken);

    // Logout: revoke the given refresh token
    Task RevokeRefreshTokenAsync(string refreshToken);
}