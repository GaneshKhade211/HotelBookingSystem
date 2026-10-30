using HotelBookingSystem.ViewModels;

namespace HotelBookingSystem.Services;

public interface IBookingService
{
    // Price/availability preview for the booking page (display only)
    Task<BookingQuoteViewModel?> GetQuoteAsync(int roomId, DateOnly checkIn, DateOnly checkOut);

    // Creates a Pending booking for userId. Concurrency-safe.
    Task<ServiceResult> CreateBookingAsync(string userId, int roomId, DateOnly checkIn, DateOnly checkOut, int guests);

    // Guest views: always filtered by userId (ownership)
    Task<BookingDetailsViewModel?> GetBookingForUserAsync(int bookingId, string userId);
    Task<List<BookingDetailsViewModel>> GetBookingsForUserAsync(string userId);
    Task<ServiceResult> CancelBookingForUserAsync(int bookingId, string userId);
}