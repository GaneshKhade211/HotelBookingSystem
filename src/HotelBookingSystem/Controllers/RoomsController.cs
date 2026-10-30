using HotelBookingSystem.Extensions;
using HotelBookingSystem.Services;
using HotelBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingSystem.Controllers;

// Public: browsing and searching rooms needs no login
[AllowAnonymous]
public class RoomsController : Controller
{
    private readonly IRoomService _rooms;
    private readonly TimeProvider _time;

    public RoomsController(IRoomService rooms, TimeProvider time)
    {
        _rooms = rooms;
        _time = time;
    }

    // GET /Rooms                  -> all active rooms
    // GET /Rooms?CheckIn=...      -> available rooms for those dates and guests
    [HttpGet]
    public async Task<IActionResult> Index(RoomSearchViewModel search)
    {
        if (!search.IsSearch)
        {
            search.Rooms = await _rooms.GetActiveRoomCardsAsync();
            return View(search);
        }

        AddStayErrors(search);
        if (!ModelState.IsValid)
        {
            return View(search); // show errors, no results
        }

        search.Rooms = await _rooms.SearchAvailableRoomsAsync(
            search.CheckIn!.Value, search.CheckOut!.Value, search.Guests!.Value);
        return View(search);
    }

    // GET /Rooms/Details/5?CheckIn=...&CheckOut=...&Guests=...
    [HttpGet]
    public async Task<IActionResult> Details(int id, RoomSearchViewModel search)
    {
        var room = await _rooms.GetRoomDetailsAsync(id);
        if (room is null || !room.IsActive)
        {
            return NotFound(); // inactive rooms are invisible to the public
        }

        var model = new PublicRoomDetailsViewModel { Room = room, Search = search };

        if (search.IsSearch)
        {
            AddStayErrors(search);
            if (ModelState.IsValid)
            {
                var checkIn = search.CheckIn!.Value;
                var checkOut = search.CheckOut!.Value;

                model.DatesValid = true;
                model.Nights = BookingRules.CountNights(checkIn, checkOut);
                model.TotalPrice = room.PricePerNight * model.Nights;
                model.FitsGuests = search.Guests!.Value <= room.Capacity;
                model.IsAvailable = await _rooms.IsRoomAvailableAsync(id, checkIn, checkOut);
            }
        }

        return View(model);
    }

    private void AddStayErrors(RoomSearchViewModel search)
    {
        if (!ModelState.IsValid)
        {
            return; // e.g. "abc" typed as a date: binding errors are already there
        }

        var today = _time.GetHotelToday();
        foreach (var (field, message) in BookingRules.ValidateStay(search.CheckIn, search.CheckOut, search.Guests, today))
        {
            ModelState.AddModelError(field, message);
        }
    }
}