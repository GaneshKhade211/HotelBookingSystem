using HotelBookingSystem.Models;
using HotelBookingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingSystem.Controllers;

// Applies to EVERY action in this controller: only users with the Admin role.
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : Controller
{
    private readonly IAdminBookingService _adminBookings;

    public AdminController(IAdminBookingService adminBookings)
    {
        _adminBookings = adminBookings;
    }

    public async Task<IActionResult> Index() =>
        View(await _adminBookings.GetDashboardAsync());
}