using System.Diagnostics;
using HotelBookingSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingSystem.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Privacy() => View();

    // Friendly page for 404, 400, ... (wired up by UseStatusCodePagesWithReExecute in Program.cs).
    // The original status code is kept, so browsers and search engines still see a real 404.
    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        return View(model: code);
    }

    // Unhandled exceptions in non-Development environments land here (no technical details shown)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}