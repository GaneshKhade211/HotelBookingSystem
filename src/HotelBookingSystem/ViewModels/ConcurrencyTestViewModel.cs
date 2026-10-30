namespace HotelBookingSystem.ViewModels;

public record ConcurrencyAttempt(int Attempt, bool Succeeded, string? Error, long ElapsedMs);

public class ConcurrencyTestViewModel
{
    public bool HasRun { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public List<ConcurrencyAttempt> Attempts { get; set; } = new();
    public int Succeeded { get; set; }
    public int BookingsSavedInDatabase { get; set; }

    public bool Passed => HasRun && Succeeded == 1 && BookingsSavedInDatabase == 1;
}