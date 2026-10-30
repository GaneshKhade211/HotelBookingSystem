using System.ComponentModel.DataAnnotations;
using HotelBookingSystem.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HotelBookingSystem.ViewModels;

// Filters for /AdminBookings (bound from the query string)
public class AdminBookingFilterViewModel
{
    public const int MaxRows = 200;

    public BookingStatus? Status { get; set; }

    [StringLength(100)]
    [Display(Name = "Search")]
    public string? Search { get; set; }

    [ValidateNever]
    public List<AdminBookingListItemViewModel> Bookings { get; set; } = new();
}

public class AdminBookingListItemViewModel
{
    public int BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public int NumberOfGuests { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus BookingStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AdminBookingDetailsViewModel
{
    public int BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
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
    public DateTime? UpdatedAtUtc { get; set; }

    // Concurrency token sent back with every status change
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<BookingStatus> AllowedTransitions { get; set; } = new();
}