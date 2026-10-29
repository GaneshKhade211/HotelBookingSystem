using HotelBookingSystem.Data;
using HotelBookingSystem.Models;
using HotelBookingSystem.Services;
using HotelBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingSystem.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ILogger<AccountController> logger)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    // ---------------- Register ----------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Normalise: trim spaces and lower-case. Identity also stores NormalizedEmail (upper-case).
        var email = model.Email.Trim().ToLowerInvariant();

        var user = new ApplicationUser
        {
            UserName = email,          // UserName = Email -> unique UserNameIndex blocks duplicates in SQL
            Email = email,
            FullName = model.FullName.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        // User + role in ONE transaction: never leave a user without a role
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var createResult = await _userManager.CreateAsync(user, model.Password); // hashes the password
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Guest); // always Guest
        if (!roleResult.Succeeded)
        {
            AddIdentityErrors(roleResult);
            return View(model); // transaction is rolled back when disposed without Commit
        }

        await transaction.CommitAsync();
        _logger.LogInformation("New guest registered: {UserId}", user.Id);

        TempData["Success"] = "Account created successfully. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    // ---------------- Login ----------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        const string invalidMessage = "Invalid email or password.";

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !user.IsActive)
        {
            // Same message for "no such user" and "wrong password": does not reveal which emails exist
            ModelState.AddModelError(string.Empty, invalidMessage);
            return View(model);
        }

        // Verifies the hash AND applies lockout (5 failures -> 15 minutes)
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Please try again in 15 minutes.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, invalidMessage);
            return View(model);
        }

        var tokens = await _tokenService.IssueTokensAsync(user);
        AuthCookies.Set(Response, tokens);

        // Open-redirect protection: only follow returnUrl if it points to THIS site
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }

    // ---------------- Logout ----------------
    // POST only (+ antiforgery), so a link or image on another site cannot log users out
    [HttpPost]
    [AllowAnonymous] // still works if the access token has already expired
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var refreshToken))
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);
        }

        AuthCookies.Clear(Response);
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    // ---------------- Access denied ----------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ---------------- Profile (protected test page) ----------------
    [HttpGet]
    [Authorize]
    public IActionResult Profile() => View();

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            // UserName = Email, so a duplicate produces two errors. Show one clear message.
            if (error.Code is "DuplicateUserName" or "DuplicateEmail")
            {
                if (!ModelState.ContainsKey("Email"))
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                }
                continue;
            }
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}