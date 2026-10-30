using HotelBookingSystem.Data;
using HotelBookingSystem.Models;
using HotelBookingSystem.Services;
using HotelBookingSystem.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using HotelBookingSystem.Middleware;

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

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are missing.");

// ---------- ASP.NET Core Identity (users, password hashing, roles) ----------
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

// ---------- Authentication: JWT read from an HttpOnly cookie ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the short claim names ("sub", "role", "name") exactly as TokenService writes them
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSettings.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },  // reject "alg" tricks
            RequireSignedTokens = true,

            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),   // default is 5 minutes, too generous

            NameClaimType = TokenService.NameClaim,
            RoleClaimType = TokenService.RoleClaim  // makes [Authorize(Roles = "Admin")] work
        };

        options.Events = new JwtBearerEvents
        {
            // 1. Read the token from our HttpOnly cookie instead of the Authorization header


            OnMessageReceived = context =>
            {
                if (context.HttpContext.Items.TryGetValue(TokenRefreshMiddleware.RefreshedAccessTokenKey, out var refreshed)
                    && refreshed is string refreshedToken)
                {
                    context.Token = refreshedToken;
                }
                else if (context.Request.Cookies.TryGetValue(AuthCookies.AccessToken, out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
           

            // 2. After signature/lifetime checks pass: require "sub" and an existing, active user
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    context.Fail("Token has no subject claim.");
                    return;
                }

                var userManager = context.HttpContext.RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(userId);

                if (user is null || !user.IsActive)
                {
                    context.Fail("User not found or inactive.");
                }
            },

            // 3. Not logged in: redirect browsers to the login page (keep 401 for /api)
            OnChallenge = context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api"))
                {
                    context.HandleResponse();
                    var returnUrl = context.Request.Path + context.Request.QueryString;
                    context.Response.Redirect("/Account/Login?returnUrl=" + Uri.EscapeDataString(returnUrl));
                }
                return Task.CompletedTask;
            },

            // 4. Logged in but wrong role: show Access Denied (keep 403 for /api)
            OnForbidden = context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.Redirect("/Account/AccessDenied");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------- Application services ----------
builder.Services.AddScoped<ITokenService, TokenService>();

// ---------- MVC ----------
// Every POST/PUT/DELETE must carry a valid antiforgery token (CSRF protection for cookie auth)
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

// ---------- Startup tasks: DB check (dev only) + seed roles ----------

//await DbInitializer.SeedRolesAsync(Services, app.Logger);
// ---------- Startup tasks: DB check (dev only), seed roles, seed dev admin ----------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // 1. Development-only database connectivity check
    if (app.Environment.IsDevelopment())
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var canConnect = await db.Database.CanConnectAsync();
        app.Logger.LogInformation("Database connection check: {CanConnect}", canConnect);
    }

    // 2. Data the app needs in every environment (idempotent)
    await DbInitializer.SeedRolesAsync(services, app.Logger);
    await DbInitializer.SeedReferenceDataAsync(services, app.Logger);

    // 3. Development-only conveniences
    if (app.Environment.IsDevelopment())
    {
        await DbInitializer.SeedDevelopmentAdminAsync(services, app.Configuration, app.Logger);
        await DbInitializer.SeedSampleRoomsAsync(services, app.Logger);
    }
}

// ---------- HTTP pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseMiddleware<TokenRefreshMiddleware>();

app.UseAuthentication();   // who are you? (reads + validates the JWT cookie)
app.UseAuthorization();    // are you allowed? ([Authorize], roles)

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();