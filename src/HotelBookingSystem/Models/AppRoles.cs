namespace HotelBookingSystem.Models;

// Single source of truth for role names, used in [Authorize(Roles = ...)] and seeding
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Guest = "Guest";

    public static readonly string[] All = { Admin, Guest };
}