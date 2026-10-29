using HotelBookingSystem.Services;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HotelBookingSystem.Middleware;

// Runs BEFORE authentication. If the access token is missing/expiring and a refresh
// cookie exists, rotate the refresh token and use the new access token for this request.
public class TokenRefreshMiddleware
{
    // Key used to hand the new access token to the JWT handler for the current request
    public const string RefreshedAccessTokenKey = "RefreshedAccessToken";

    // Refresh slightly before expiry so a request never arrives with a just-expired token
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromSeconds(30);

    private readonly RequestDelegate _next;
    private readonly ILogger<TokenRefreshMiddleware> _logger;

    public TokenRefreshMiddleware(RequestDelegate next, ILogger<TokenRefreshMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // ITokenService is scoped, so it is injected per request here, not in the constructor
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
    {
        if (ShouldTryRefresh(context, out var refreshToken))
        {
            var tokens = await tokenService.RotateRefreshTokenAsync(refreshToken);

            if (tokens is not null)
            {
                AuthCookies.Set(context.Response, tokens);
                context.Items[RefreshedAccessTokenKey] = tokens.AccessToken;
                _logger.LogInformation("Access token refreshed using refresh-token rotation.");
            }
            else
            {
                // Invalid, expired, revoked or reused refresh token: sign the browser out
                AuthCookies.Clear(context.Response);
                _logger.LogInformation("Refresh token rejected. Authentication cookies cleared.");
            }
        }

        await _next(context);
    }

    private static bool ShouldTryRefresh(HttpContext context, out string refreshToken)
    {
        refreshToken = string.Empty;

        // Only for MVC controller actions, not for static files (css, js, images)
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action is null)
        {
            return false;
        }

        // Logout revokes the refresh token itself; do not rotate it first
        if (action.ControllerName == "Account" && action.ActionName == "Logout")
        {
            return false;
        }

        if (!context.Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var cookieValue)
            || string.IsNullOrEmpty(cookieValue))
        {
            return false; // not logged in
        }
        refreshToken = cookieValue;

        // Access cookie gone (the browser deletes it when it expires) -> refresh
        if (!context.Request.Cookies.TryGetValue(AuthCookies.AccessToken, out var accessToken)
            || string.IsNullOrEmpty(accessToken))
        {
            return true;
        }

        // Access token present: refresh only if it expires within the threshold.
        // Reading "exp" without validating is fine here: the JWT handler still fully validates
        // whichever token is finally used.
        try
        {
            var jwt = new JsonWebToken(accessToken);
            return jwt.ValidTo <= DateTime.UtcNow.Add(RefreshThreshold);
        }
        catch (Exception)
        {
            return true; // malformed token -> try to replace it
        }
    }
}