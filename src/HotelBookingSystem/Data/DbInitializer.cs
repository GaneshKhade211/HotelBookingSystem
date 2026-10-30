using HotelBookingSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSystem.Data;

public static class DbInitializer
{
    // ---------------- Roles (all environments, idempotent) ----------------
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

    // ---------------- Room types + amenities (all environments, idempotent) ----------------
    // Reference data the application needs in order to work. Adds only names that are missing.
    public static async Task SeedReferenceDataAsync(IServiceProvider services, ILogger logger)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        var roomTypes = new (string Name, string Description)[]
        {
            ("Standard", "Comfortable room with all essential amenities."),
            ("Deluxe",   "Spacious room with upgraded furnishings and a city view."),
            ("Suite",    "Separate living area, premium bathroom and extra space."),
            ("Family",   "Large room with extra beds, ideal for families.")
        };

        var amenities = new[]
        {
            "Wi-Fi", "Air conditioning", "Television", "Mini bar",
            "Work desk", "Bathtub", "Balcony", "Room service"
        };

        var existingTypes = await db.RoomTypes.Select(t => t.Name).ToListAsync();
        foreach (var (name, description) in roomTypes.Where(t => !existingTypes.Contains(t.Name)))
        {
            db.RoomTypes.Add(new RoomType { Name = name, Description = description });
        }

        var existingAmenities = await db.RoomAmenities.Select(a => a.Name).ToListAsync();
        foreach (var name in amenities.Where(a => !existingAmenities.Contains(a)))
        {
            db.RoomAmenities.Add(new RoomAmenity { Name = name });
        }

        var added = await db.SaveChangesAsync();
        if (added > 0)
        {
            logger.LogInformation("Seeded {Count} room type/amenity rows", added);
        }
    }

    // ---------------- Sample rooms (DEVELOPMENT ONLY, only if no rooms exist) ----------------
    public static async Task SeedSampleRoomsAsync(IServiceProvider services, ILogger logger)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        if (await db.Rooms.AnyAsync())
        {
            return; // never overwrite or duplicate real data
        }

        var types = await db.RoomTypes.ToDictionaryAsync(t => t.Name);
        var amenities = await db.RoomAmenities.ToDictionaryAsync(a => a.Name);

        Room MakeRoom(string number, string type, int capacity, decimal price, string description,
                      bool isActive, params string[] amenityNames) => new()
                      {
                          RoomNumber = number,
                          RoomType = types[type],
                          Capacity = capacity,
                          PricePerNight = price,
                          Description = description,
                          IsActive = isActive,
                          AmenityMappings = amenityNames
                .Select(name => new RoomAmenityMapping { Amenity = amenities[name] })
                .ToList()
                      };

        var rooms = new[]
        {
            MakeRoom("101", "Standard", 2, 2500m, "Quiet standard room facing the garden.", true,
                     "Wi-Fi", "Air conditioning", "Television"),
            MakeRoom("102", "Standard", 2, 2500m, "Standard room close to the lift.", true,
                     "Wi-Fi", "Air conditioning", "Television", "Work desk"),
            MakeRoom("103", "Standard", 1, 1800m, "Compact single room. Currently under renovation.", false,
                     "Wi-Fi", "Air conditioning"),
            MakeRoom("201", "Deluxe", 3, 4000m, "Deluxe room with a city view and a work desk.", true,
                     "Wi-Fi", "Air conditioning", "Television", "Mini bar", "Work desk"),
            MakeRoom("202", "Deluxe", 3, 4200m, "Corner deluxe room with a balcony.", true,
                     "Wi-Fi", "Air conditioning", "Television", "Mini bar", "Balcony"),
            MakeRoom("301", "Suite", 4, 7500m, "Top-floor suite with a separate living room and bathtub.", true,
                     "Wi-Fi", "Air conditioning", "Television", "Mini bar", "Bathtub", "Balcony", "Room service"),
            MakeRoom("302", "Family", 5, 6000m, "Large family room with two double beds and a sofa bed.", true,
                     "Wi-Fi", "Air conditioning", "Television", "Room service")
        };

        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} sample rooms", rooms.Length);
    }

    // ---------------- Development admin (from User Secrets) ----------------
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