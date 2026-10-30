namespace HotelBookingSystem.ViewModels;

// Expanded with occupancy/booking statistics in Phase 10
public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalRooms { get; set; }
    public int ActiveRooms { get; set; }
    public int TotalBookings { get; set; }
}