using System.Security.Claims;
using HotelBookingSystem.Models;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HotelBookingSystem.Extensions;

public static class ClaimsPrincipalExtensions
{
    // The user id ALWAYS comes from the validated JWT ("sub" claim),
    // never from a form field, query string or route value.
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new InvalidOperationException("Authenticated user has no 'sub' claim.");

    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.Admin);
}