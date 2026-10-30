namespace HotelBookingSystem.Extensions;

public static class TimeProviderExtensions
{
    // Booking dates follow the hotel's calendar (India Standard Time), not the server's
    // time zone, so "today" is correct even if the server runs in UTC.
    private static readonly TimeZoneInfo HotelTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateOnly GetHotelToday(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), HotelTimeZone).DateTime);
}