using System.Diagnostics;
using HotelBookingSystem.Data;
using HotelBookingSystem.Extensions;
using HotelBookingSystem.Models;
using HotelBookingSystem.Services;
using HotelBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSystem.Controllers;

// DEVELOPMENT-ONLY diagnostics for the internship review. Returns 404 in any other environment.
[Authorize(Roles = AppRoles.Admin)]
public class DevToolsController : Controller
{
    // Test bookings use dates far in the future so they never clash with real bookings
    private const int TestDaysAhead = 300;

    private readonly ApplicationDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _env;
    private readonly TimeProvider _time;

    public DevToolsController(ApplicationDbContext db, IServiceScopeFactory scopeFactory,
                              IWebHostEnvironment env, TimeProvider time)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _env = env;
        _time = time;
    }

    [HttpGet]
    public IActionResult ConcurrencyTest() =>
        _env.IsDevelopment() ? View(new ConcurrencyTestViewModel()) : NotFound();

    [HttpPost]
    public async Task<IActionResult> ConcurrencyTest(int attempts = 10)
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        attempts = Math.Clamp(attempts, 2, 50);

        var room = await _db.Rooms.AsNoTracking()
            .Where(r => r.IsActive && r.Capacity >= 2)
            .OrderBy(r => r.RoomId)
            .FirstOrDefaultAsync();

        var guestRoleId = await _db.Roles.Where(r => r.Name == AppRoles.Guest).Select(r => r.Id).SingleAsync();
        var guestId = await _db.UserRoles.Where(ur => ur.RoleId == guestRoleId).Select(ur => ur.UserId).FirstOrDefaultAsync();

        if (room is null || guestId is null)
        {
            TempData["Error"] = "The test needs at least one active room (capacity 2+) and one guest account.";
            return RedirectToAction(nameof(ConcurrencyTest));
        }

        var (checkIn, checkOut) = GetTestDates();
        await DeleteTestBookingsAsync(room.RoomId, checkIn, checkOut); // start clean

        // All requests wait at this gate, then start at the same instant
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(1, attempts).Select(async attempt =>
        {
            // Separate scope = separate DbContext = separate SQL connection, like separate users
            await using var scope = _scopeFactory.CreateAsyncScope();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

            await startGate.Task;
            var stopwatch = Stopwatch.StartNew();
            var result = await bookingService.CreateBookingAsync(guestId, room.RoomId, checkIn, checkOut, 2);

            return new ConcurrencyAttempt(attempt, result.Succeeded, result.Error, stopwatch.ElapsedMilliseconds);
        }).ToList();

        startGate.SetResult();                 // GO
        var results = await Task.WhenAll(tasks);

        var model = new ConcurrencyTestViewModel
        {
            HasRun = true,
            RoomNumber = room.RoomNumber,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Attempts = results.OrderBy(r => r.ElapsedMs).ToList(),
            Succeeded = results.Count(r => r.Succeeded),
            BookingsSavedInDatabase = await _db.Bookings.CountAsync(b =>
                b.RoomId == room.RoomId && b.CheckInDate == checkIn && b.CheckOutDate == checkOut)
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CleanupConcurrencyTest()
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        var (checkIn, checkOut) = GetTestDates();
        var deleted = await _db.Bookings
            .Where(b => b.CheckInDate == checkIn && b.CheckOutDate == checkOut)
            .ExecuteDeleteAsync();

        TempData["Success"] = $"Removed {deleted} test booking(s).";
        return RedirectToAction(nameof(ConcurrencyTest));
    }

    private (DateOnly CheckIn, DateOnly CheckOut) GetTestDates()
    {
        var checkIn = _time.GetHotelToday().AddDays(TestDaysAhead);
        return (checkIn, checkIn.AddDays(2));
    }

    private Task<int> DeleteTestBookingsAsync(int roomId, DateOnly checkIn, DateOnly checkOut) =>
        _db.Bookings
            .Where(b => b.RoomId == roomId && b.CheckInDate == checkIn && b.CheckOutDate == checkOut)
            .ExecuteDeleteAsync();
}