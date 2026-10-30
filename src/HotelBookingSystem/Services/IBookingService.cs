using HotelBookingSystem.ViewModels;

namespace HotelBookingSystem.Services;

public interface IBookingService
{
    // Price/availability preview for the booking page (display only)
    Task<BookingQuoteViewModel?> GetQuoteAsync(int roomId, DateOnly checkIn, DateOnly checkOut);

    // Creates a Pending booking for userId. Concurrency-safe.
    Task<ServiceResult> CreateBookingAsync(string userId, int roomId, DateOnly checkIn, DateOnly checkOut, int guests);

    // Returns the booking ONLY if it belongs to userId (ownership check)
    Task<BookingDetailsViewModel?> GetBookingForUserAsync(int bookingId, string userId);
}