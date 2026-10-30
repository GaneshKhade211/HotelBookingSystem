using System.ComponentModel.DataAnnotations;
using HotelBookingSystem.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HotelBookingSystem.ViewModels;

// The ONLY values the browser may send when booking. No price, no user id, no status.
public class CreateBookingViewModel
{
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Please choose a check-in date.")]
    [Display(Name = "Check-in")]
    public DateOnly? CheckIn { get; set; }

    [Required(ErrorMessage = "Please choose a check-out date.")]
    [Display(Name = "Check-out")]
    public DateOnly? CheckOut { get; set; }

    [Required(ErrorMessage = "Please enter the number of guests.")]
    public int? Guests { get; set; }

    // Calculated by the server for display only. [BindNever]: cannot be posted by the browser.
    [BindNever, ValidateNever]
    public BookingQuoteViewModel? Quote { get; set; }
}

public class BookingQuoteViewModel
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal PricePerNight { get; set; }
    public string? ImageUrl { get; set; }
    public int Nights { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsAvailable { get; set; }
}

public class BookingDetailsViewModel
{
    public int BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public int NumberOfNights { get; set; }
    public decimal PricePerNight { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus BookingStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool CanCancel { get; set; }
}