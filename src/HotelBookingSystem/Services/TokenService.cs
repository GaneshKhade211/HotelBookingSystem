using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HotelBookingSystem.Data;
using HotelBookingSystem.Models;
using HotelBookingSystem.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HotelBookingSystem.Services;

public class TokenService : ITokenService
{
    // Short claim names used inside the JWT. Step 5.4 configures validation to read these.
    public const string RoleClaim = "role";
    public const string NameClaim = "name";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtSettings _jwt;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IOptions<JwtSettings> jwtOptions,
        ILogger<TokenService> logger)
    {
        _db = db;
        _userManager = userManager;
        _jwt = jwtOptions.Value;
        _logger = logger;
    }

    // ---------------- Login ----------------
    public async Task<AuthTokens> IssueTokensAsync(ApplicationUser user)
    {
        var (accessToken, accessExpiresAtUtc) = await CreateAccessTokenAsync(user);
        var (rawRefreshToken, refreshEntity) = CreateRefreshToken(user.Id);

        _db.RefreshTokens.Add(refreshEntity);
        await _db.SaveChangesAsync();

        return new AuthTokens(accessToken, accessExpiresAtUtc, rawRefreshToken, refreshEntity.ExpiresAtUtc);
    }

    // ---------------- Refresh (rotation) ----------------
    public async Task<AuthTokens?> RotateRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var hash = HashToken(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is null)
        {
            return null; // unknown token
        }

        // Reuse detection: a revoked token should never come back. Assume theft.
        if (stored.RevokedAtUtc is not null)
        {
            _logger.LogWarning("Refresh token reuse detected for user {UserId}. Revoking all their tokens.", stored.UserId);
            await RevokeAllForUserAsync(stored.UserId);
            return null;
        }

        if (stored.IsExpired)
        {
            return null;
        }

        // Inactive or locked-out users cannot refresh
        if (!stored.User.IsActive || await _userManager.IsLockedOutAsync(stored.User))
        {
            await RevokeAllForUserAsync(stored.UserId);
            return null;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        // Atomic revoke: only succeeds if no other request has revoked this token first.
        // This prevents two simultaneous refreshes from both getting new tokens.
        DateTime? now = DateTime.UtcNow;
        var revokedRows = await _db.RefreshTokens
            .Where(t => t.RefreshTokenId == stored.RefreshTokenId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now));

        if (revokedRows == 0)
        {
            await transaction.RollbackAsync();
            return null; // lost the race to a parallel request
        }

        var (rawRefreshToken, newEntity) = CreateRefreshToken(stored.UserId);
        _db.RefreshTokens.Add(newEntity);
        await _db.SaveChangesAsync();

        // Link old -> new (rotation chain, useful for auditing)
        int? newId = newEntity.RefreshTokenId;
        await _db.RefreshTokens
            .Where(t => t.RefreshTokenId == stored.RefreshTokenId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedByTokenId, newId));

        await transaction.CommitAsync();

        var (accessToken, accessExpiresAtUtc) = await CreateAccessTokenAsync(stored.User);
        return new AuthTokens(accessToken, accessExpiresAtUtc, rawRefreshToken, newEntity.ExpiresAtUtc);
    }

    // ---------------- Logout ----------------
    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = HashToken(refreshToken);
        DateTime? now = DateTime.UtcNow;

        await _db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now));
    }

    // ---------------- Helpers ----------------
    private async Task<(string Token, DateTime ExpiresAtUtc)> CreateAccessTokenAsync(ApplicationUser user)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),            // user id: ownership checks use this
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(NameClaim, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())  // unique token id
        };

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(RoleClaim, role)));

        var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(_jwt.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiresAtUtc);
    }

    private (string RawToken, RefreshToken Entity) CreateRefreshToken(string userId)
    {
        // 64 cryptographically random bytes, URL-safe Base64 encoded
        var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        var now = DateTime.UtcNow;

        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_jwt.RefreshTokenDays)
        };

        return (rawToken, entity);
    }

    // SHA-256 -> 64 hex characters (fits TokenHash nvarchar(64))
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task RevokeAllForUserAsync(string userId)
    {
        DateTime? now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now));
    }
}