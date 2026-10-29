using HotelBookingSystem.Data;
using HotelBookingSystem.Models;
using HotelBookingSystem.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---------- JWT settings (validated at startup) ----------
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------- ASP.NET Core Identity (users, password hashing, roles) ----------
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Duplicate accounts: also enforced by the unique UserNameIndex (UserName = Email)
        options.User.RequireUniqueEmail = true;

        // Password policy
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Brute-force protection: lock the account for 15 minutes after 5 failed logins
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

// ---------- MVC ----------
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ---------- Startup tasks: DB check (dev only) + seed roles ----------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    if (app.Environment.IsDevelopment())
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var canConnect = await db.Database.CanConnectAsync();
        app.Logger.LogInformation("Database connection check: {CanConnect}", canConnect);
    }

    await DbInitializer.SeedRolesAsync(services, app.Logger);
}

// ---------- HTTP pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();