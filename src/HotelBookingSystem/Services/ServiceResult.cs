namespace HotelBookingSystem.Services;

// Lets a service report a business-rule failure (and which form field it belongs to)
// without throwing exceptions or knowing anything about MVC.
public class ServiceResult
{
    public bool Succeeded { get; private init; }
    public string? Error { get; private init; }
    public string? Field { get; private init; }
    public int? Id { get; private init; }

    public static ServiceResult Success(int? id = null) =>
        new() { Succeeded = true, Id = id };

    public static ServiceResult Fail(string error, string? field = null) =>
        new() { Succeeded = false, Error = error, Field = field };
}