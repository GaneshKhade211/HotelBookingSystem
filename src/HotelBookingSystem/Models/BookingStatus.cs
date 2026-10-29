namespace HotelBookingSystem.Models;

// Stored as text in SQL Server (configured in ApplicationDbContext) so the
// database stays readable, e.g. 'Confirmed' instead of 1.
public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed
}