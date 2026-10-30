using HotelBookingSystem.Extensions;
using HotelBookingSystem.Models;
using HotelBookingSystem.Services;
using HotelBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingSystem.Controllers;

// Only logged-in Guests can book. Not logged in -> Login page; Admin -> Access Denied.
[Authorize(Roles = AppRoles.Guest)]
public class BookingsController : Controller
{
    private readonly IBookingService _bookings;
    private readonly TimeProvider _time;

    public BookingsController(IBookingService bookings, TimeProvider time)
    {
        _bookings = bookings;
        _time = time;
    }

    // GET /Bookings/Create?roomId=4&checkIn=2026-11-09&checkOut=2026-11-12&guests=2
    [HttpGet]
    public async Task<IActionResult> Create(int roomId, DateOnly? checkIn, DateOnly? checkOut, int? guests)
    {
        var model = new CreateBookingViewModel
        {
            RoomId = roomId,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Guests = guests
        };

        await ValidateAndQuoteAsync(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateBookingViewModel model)
    {
        if (await ValidateAndQuoteAsync(model))
        {
            // The user id comes from the validated JWT, never from the form
            var result = await _bookings.CreateBookingAsync(
                User.GetUserId(), model.RoomId, model.CheckIn!.Value, model.CheckOut!.Value, model.Guests!.Value);

            if (result.Succeeded)
            {
                TempData["Success"] = "Your booking has been created.";
                return RedirectToAction(nameof(Confirmation), new { id = result.Id });
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var booking = await _bookings.GetBookingForUserAsync(id, User.GetUserId());
        return booking is null ? NotFound() : View(booking);
    }

    // Shared validation + server-side quote for GET and POST
    private async Task<bool> ValidateAndQuoteAsync(CreateBookingViewModel model)
    {
        if (ModelState.IsValid)
        {
            var today = _time.GetHotelToday();
            foreach (var (field, message) in BookingRules.ValidateStay(model.CheckIn, model.CheckOut, model.Guests, today))
            {
                ModelState.AddModelError(field, message);
            }
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        model.Quote = await _bookings.GetQuoteAsync(model.RoomId, model.CheckIn!.Value, model.CheckOut!.Value);

        if (model.Quote is null)
        {
            ModelState.AddModelError(string.Empty, "This room is not available for booking.");
            return false;
        }

        if (model.Guests!.Value > model.Quote.Capacity)
        {
            ModelState.AddModelError("Guests", $"This room fits up to {model.Quote.Capacity} guest(s).");
            return false;
        }

        if (!model.Quote.IsAvailable)
        {
            ModelState.AddModelError(string.Empty, "This room is not available for these dates.");
            return false;
        }

        return true;
    }
}
