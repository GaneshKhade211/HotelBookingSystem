using HotelBookingSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSystem.Data;

// IdentityDbContext adds the AspNetUsers, AspNetRoles, AspNetUserRoles, ... tables
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomAmenity> RoomAmenities => Set<RoomAmenity>();
    public DbSet<RoomAmenityMapping> RoomAmenityMappings => Set<RoomAmenityMapping>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Must run first: configures all the Identity tables
        base.OnModelCreating(builder);

        // ---------- RoomTypes ----------
        builder.Entity<RoomType>(e =>
        {
            e.HasIndex(t => t.Name).IsUnique();
        });

        // ---------- Rooms ----------
        builder.Entity<Room>(e =>
        {
            e.HasIndex(r => r.RoomNumber).IsUnique();
            e.Property(r => r.PricePerNight).HasPrecision(10, 2);

            e.HasOne(r => r.RoomType)
             .WithMany(t => t.Rooms)
             .HasForeignKey(r => r.RoomTypeId)
             .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Rooms_Capacity", "[Capacity] BETWEEN 1 AND 10");
                t.HasCheckConstraint("CK_Rooms_PricePerNight", "[PricePerNight] > 0");
            });
        });

        // ---------- RoomAmenities ----------
        builder.Entity<RoomAmenity>(e =>
        {
            e.HasIndex(a => a.Name).IsUnique();
        });

        // ---------- RoomAmenityMappings (many-to-many) ----------
        builder.Entity<RoomAmenityMapping>(e =>
        {
            e.HasKey(m => new { m.RoomId, m.AmenityId });   // composite primary key

            e.HasOne(m => m.Room)
             .WithMany(r => r.AmenityMappings)
             .HasForeignKey(m => m.RoomId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Amenity)
             .WithMany(a => a.RoomMappings)
             .HasForeignKey(m => m.AmenityId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Bookings ----------
        builder.Entity<Booking>(e =>
        {
            e.HasIndex(b => b.BookingReference).IsUnique();

            // Speeds up the availability (overlap) check for a room
            e.HasIndex(b => new { b.RoomId, b.CheckInDate, b.CheckOutDate });

            e.Property(b => b.PricePerNight).HasPrecision(10, 2);
            e.Property(b => b.TotalAmount).HasPrecision(10, 2);

            // Store the enum as readable text ('Confirmed') instead of a number
            e.Property(b => b.BookingStatus)
             .HasConversion<string>()
             .HasMaxLength(20);

            e.HasOne(b => b.User)
             .WithMany(u => u.Bookings)
             .HasForeignKey(b => b.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Room)
             .WithMany(r => r.Bookings)
             .HasForeignKey(b => b.RoomId)
             .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Bookings_Dates", "[CheckOutDate] > [CheckInDate]");
                t.HasCheckConstraint("CK_Bookings_Guests", "[NumberOfGuests] BETWEEN 1 AND 10");
                t.HasCheckConstraint("CK_Bookings_Nights", "[NumberOfNights] BETWEEN 1 AND 30");
                t.HasCheckConstraint("CK_Bookings_PricePerNight", "[PricePerNight] > 0");
                t.HasCheckConstraint("CK_Bookings_TotalAmount", "[TotalAmount] >= 0");
                t.HasCheckConstraint("CK_Bookings_Status",
                    "[BookingStatus] IN ('Pending', 'Confirmed', 'Cancelled', 'Completed')");
            });
        });

        // ---------- Payments ----------
        builder.Entity<Payment>(e =>
        {
            e.HasIndex(p => p.TransactionReference).IsUnique();
            e.Property(p => p.Amount).HasPrecision(10, 2);

            e.Property(p => p.PaymentStatus)
             .HasConversion<string>()
             .HasMaxLength(20);

            e.HasOne(p => p.Booking)
             .WithMany(b => b.Payments)
             .HasForeignKey(p => p.BookingId)
             .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                t.HasCheckConstraint("CK_Payments_Status",
                    "[PaymentStatus] IN ('Pending', 'Succeeded', 'Failed', 'Refunded')");
            });
        });

        // ---------- RefreshTokens ----------
        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();

            // Tokens are useless without their user, so delete them with the user
            e.HasOne(t => t.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // Self-reference for rotation chains; NoAction avoids SQL Server
            // "multiple cascade paths" errors
            e.HasOne(t => t.ReplacedByToken)
             .WithMany()
             .HasForeignKey(t => t.ReplacedByTokenId)
             .OnDelete(DeleteBehavior.NoAction);
        });
    }
}