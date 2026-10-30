using HotelBookingSystem.Models;
using Microsoft.AspNetCore.Identity;

namespace HotelBookingSystem.Data;

public static class DbInitializer
{
    // Idempotent: creates only the roles that do not exist yet, so it is safe to run on every startup.
    public static async Task SeedRolesAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create role '{role}': {Describe(result)}");
            }

            logger.LogInformation("Created role {Role}", role);
        }
    }

    // DEVELOPMENT ONLY. Reads the admin account from configuration (User Secrets), never from code.
    // If SeedAdmin:Email / SeedAdmin:Password are not configured, nothing is created.
    // An existing user's password is never changed.
    public static async Task SeedDevelopmentAdminAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        var fullName = config["SeedAdmin:FullName"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("SeedAdmin is not configured. Skipping development admin creation.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        email = email.Trim().ToLowerInvariant();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName.Trim(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            // Same Identity pipeline as normal registration: password policy + hashing
            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create the development admin: {Describe(createResult)}");
            }

            logger.LogInformation("Created development admin account {Email}", email);
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to add the Admin role: {Describe(roleResult)}");
            }

            logger.LogInformation("Added {Email} to the Admin role", email);
        }
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}