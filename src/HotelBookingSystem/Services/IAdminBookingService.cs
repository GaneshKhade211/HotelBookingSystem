using HotelBookingSystem.Models;
using HotelBookingSystem.ViewModels;

namespace HotelBookingSystem.Services;

// Admin-only operations across ALL bookings (no ownership filter)
public interface IAdminBookingService
{
    Task<AdminDashboardViewModel> GetDashboardAsync();
    Task<List<AdminBookingListItemViewModel>> GetBookingsAsync(AdminBookingFilterViewModel filter);
    Task<AdminBookingDetailsViewModel?> GetBookingAsync(int bookingId);
    Task<ServiceResult> UpdateStatusAsync(int bookingId, BookingStatus newStatus, byte[] rowVersion);
}