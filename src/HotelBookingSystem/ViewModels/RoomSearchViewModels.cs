using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HotelBookingSystem.ViewModels;

// Bound from the query string: /Rooms?CheckIn=2026-11-10&CheckOut=2026-11-13&Guests=2
public class RoomSearchViewModel
{
    [Display(Name = "Check-in")]
    public DateOnly? CheckIn { get; set; }

    [Display(Name = "Check-out")]
    public DateOnly? CheckOut { get; set; }

    [Display(Name = "Guests")]
    public int? Guests { get; set; }

    public bool IsSearch => CheckIn.HasValue || CheckOut.HasValue || Guests.HasValue;

    [ValidateNever]
    public List<RoomCardViewModel> Rooms { get; set; } = new();
}

// One room card on the listing/search page
public class RoomCardViewModel
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public decimal PricePerNight { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Amenities { get; set; } = new();
    public int Nights { get; set; }          // 0 when browsing without dates
    public decimal TotalPrice { get; set; }  // calculated on the server
}

// Public room details page, with optional stay dates
public class PublicRoomDetailsViewModel
{
    public RoomDetailsViewModel Room { get; set; } = null!;
    public RoomSearchViewModel Search { get; set; } = new();
    public bool DatesValid { get; set; }
    public int Nights { get; set; }
    public decimal TotalPrice { get; set; }
    public bool FitsGuests { get; set; }
    public bool IsAvailable { get; set; }
}