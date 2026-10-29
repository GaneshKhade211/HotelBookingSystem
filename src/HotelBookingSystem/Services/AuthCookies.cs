namespace HotelBookingSystem.Services;

// Writes/clears the two authentication cookies with secure settings.
public static class AuthCookies
{
    // "__Host-" prefix: the browser only accepts the cookie if it is Secure, has Path=/
    // and no Domain, so it cannot be set or overwritten by another (sub)domain.
    public const string AccessToken = "__Host-hb_access";
    public const string RefreshToken = "__Host-hb_refresh";

    public static void Set(HttpResponse response, AuthTokens tokens)
    {
        response.Cookies.Append(AccessToken, tokens.AccessToken, Build(tokens.AccessTokenExpiresAtUtc));
        response.Cookies.Append(RefreshToken, tokens.RefreshToken, Build(tokens.RefreshTokenExpiresAtUtc));
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(AccessToken, Build(DateTime.UnixEpoch));
        response.Cookies.Delete(RefreshToken, Build(DateTime.UnixEpoch));
    }

    private static CookieOptions Build(DateTime expiresAtUtc) => new()
    {
        HttpOnly = true,                 // JavaScript cannot read it, so XSS cannot steal it
        Secure = true,                   // only sent over HTTPS
        SameSite = SameSiteMode.Strict,  // not sent on cross-site requests (CSRF defence)
        Path = "/",
        IsEssential = true,
        Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero)
    };
}