using Microsoft.EntityFrameworkCore;

namespace HotelBookingSystem.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSet<T> properties for Rooms, Bookings, etc. will be added in Phase 4.
}