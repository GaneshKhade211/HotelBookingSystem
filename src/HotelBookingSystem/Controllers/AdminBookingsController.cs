using HotelBookingSystem.Models;
using HotelBookingSystem.Services;
using HotelBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingSystem.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminBookingsController : Controller
{
    private readonly IAdminBookingService _bookings;

    public AdminBookingsController(IAdminBookingService bookings)
    {
        _bookings = bookings;
    }

    // GET /AdminBookings?Status=Pending&Search=HB-2610
    [HttpGet]
    public async Task<IActionResult> Index(AdminBookingFilterViewModel filter)
    {
        if (ModelState.IsValid)
        {
            filter.Bookings = await _bookings.GetBookingsAsync(filter);
        }
        return View(filter);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var booking = await _bookings.GetBookingAsync(id);
        return booking is null ? NotFound() : View(booking);
    }

    // POST /AdminBookings/UpdateStatus/5   (newStatus + rowVersion from hidden fields)
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, BookingStatus newStatus, string rowVersion)
    {
        if (!Enum.IsDefined(newStatus) || string.IsNullOrEmpty(rowVersion))
        {
            return BadRequest();
        }

        byte[] version;
        try
        {
            version = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            return BadRequest();
        }

        var result = await _bookings.UpdateStatusAsync(id, newStatus, version);
        if (result.Succeeded)
        {
            TempData["Success"] = $"Booking status changed to {newStatus}.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}