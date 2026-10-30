using HotelBookingSystem.Data;
using HotelBookingSystem.Models;
using HotelBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSystem.Controllers;

// Applies to EVERY action in this controller: only users with the Admin role.
// Not logged in -> redirected to Login. Logged in as Guest -> Access Denied.
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var model = new AdminDashboardViewModel
        {
            TotalUsers = await _db.Users.CountAsync(),
            TotalRooms = await _db.Rooms.CountAsync(),
            ActiveRooms = await _db.Rooms.CountAsync(r => r.IsActive),
            TotalBookings = await _db.Bookings.CountAsync()
        };

        return View(model);
    }
}