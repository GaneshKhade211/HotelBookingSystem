namespace HotelBookingSystem.ViewModels;

public class AdminDashboardViewModel
{
    // Totals
    public int TotalUsers { get; set; }
    public int TotalRooms { get; set; }
    public int ActiveRooms { get; set; }
    public int TotalBookings { get; set; }

    // Bookings by status
    public int PendingBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int CompletedBookings { get; set; }

    // Today (hotel time zone)
    public DateOnly Today { get; set; }
    public int OccupiedTonight { get; set; }
    public decimal OccupancyPercent { get; set; }
    public int CheckInsToday { get; set; }
    public int CheckOutsToday { get; set; }

    // Total value of confirmed + completed bookings (no payments are processed)
    public decimal BookedValue { get; set; }

    public List<AdminBookingListItemViewModel> UpcomingArrivals { get; set; } = new();
}