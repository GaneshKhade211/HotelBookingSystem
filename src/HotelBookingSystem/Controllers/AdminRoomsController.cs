using HotelBookingSystem.Models;
using HotelBookingSystem.Services;
using HotelBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingSystem.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminRoomsController : Controller
{
    private readonly IRoomService _rooms;

    public AdminRoomsController(IRoomService rooms)
    {
        _rooms = rooms;
    }

    public async Task<IActionResult> Index() =>
        View(await _rooms.GetAllRoomsAsync());

    public async Task<IActionResult> Details(int id)
    {
        var model = await _rooms.GetRoomDetailsAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new RoomFormViewModel();
        await _rooms.PopulateFormOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(RoomFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _rooms.CreateRoomAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "Room created successfully.";
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        await _rooms.PopulateFormOptionsAsync(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _rooms.GetRoomForEditAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, RoomFormViewModel model)
    {
        // The id in the URL and the hidden field must agree
        if (id != model.RoomId)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            var result = await _rooms.UpdateRoomAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "Room updated successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        await _rooms.PopulateFormOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        var result = await _rooms.SetActiveAsync(id, isActive);
        if (!result.Succeeded)
        {
            return NotFound();
        }

        TempData["Success"] = isActive
            ? "Room activated. It can now receive new bookings."
            : "Room deactivated. It will not receive new bookings; existing bookings are unchanged.";
        return RedirectToAction(nameof(Index));
    }
}