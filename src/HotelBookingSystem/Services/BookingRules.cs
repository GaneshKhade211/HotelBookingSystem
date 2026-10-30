using System.Linq.Expressions;
using HotelBookingSystem.Models;

namespace HotelBookingSystem.Services;

// Booking policy in ONE place. Used by search (Phase 8) and booking creation (Phase 9).
public static class BookingRules
{
    public const int MaxNights = 30;
    public const int MaxGuests = 10;
    public const int MaxDaysAhead = 365;

    // Overlap rule for date-only stays, counting only statuses that hold a room:
    //   ExistingCheckIn < RequestedCheckOut AND ExistingCheckOut > RequestedCheckIn
    // Strict < and > allow same-day turnover (one guest checks out the day another checks in).
    public static Expression<Func<Booking, bool>> BlocksStay(DateOnly checkIn, DateOnly checkOut) =>
        b => (b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Confirmed)
             && b.CheckInDate < checkOut
             && b.CheckOutDate > checkIn;

    public static int CountNights(DateOnly checkIn, DateOnly checkOut) =>
        checkOut.DayNumber - checkIn.DayNumber;

    // Returns (field, message) pairs; an empty list means the stay is valid.
    public static List<(string Field, string Message)> ValidateStay(
        DateOnly? checkIn, DateOnly? checkOut, int? guests, DateOnly today)
    {
        var errors = new List<(string, string)>();

        if (checkIn is null) errors.Add(("CheckIn", "Please choose a check-in date."));
        if (checkOut is null) errors.Add(("CheckOut", "Please choose a check-out date."));

        if (guests is null)
            errors.Add(("Guests", "Please enter the number of guests."));
        else if (guests < 1 || guests > MaxGuests)
            errors.Add(("Guests", $"Guests must be between 1 and {MaxGuests}."));

        if (checkIn is { } inDate && checkOut is { } outDate)
        {
            if (inDate < today)
                errors.Add(("CheckIn", "Check-in cannot be in the past."));
            else if (inDate > today.AddDays(MaxDaysAhead))
                errors.Add(("CheckIn", $"Bookings can be made up to {MaxDaysAhead} days ahead."));

            if (outDate <= inDate)
                errors.Add(("CheckOut", "Check-out must be after check-in."));
            else if (CountNights(inDate, outDate) > MaxNights)
                errors.Add(("CheckOut", $"A stay can be at most {MaxNights} nights."));
        }

        return errors;
    }
}