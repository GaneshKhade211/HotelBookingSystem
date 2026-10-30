using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HotelBookingSystem.ViewModels;

// Used by the Add and Edit room forms. Only editable fields are bindable.
public class RoomFormViewModel
{
    public int RoomId { get; set; }

    [Required, StringLength(10)]
    [RegularExpression(@"^\s*[A-Za-z0-9-]+\s*$", ErrorMessage = "Use letters, digits and hyphens only.")]
    [Display(Name = "Room number")]
    public string RoomNumber { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a room type.")]
    [Display(Name = "Room type")]
    public int RoomTypeId { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(1, 10)]
    public int Capacity { get; set; } = 2;

    [Range(typeof(decimal), "1", "1000000", ErrorMessage = "Price must be between ₹1 and ₹10,00,000.")]
    [Display(Name = "Price per night (₹)")]
    public decimal PricePerNight { get; set; }

    [Display(Name = "Active (available for new bookings)")]
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }

    public List<int> SelectedAmenityIds { get; set; } = new();

    // Dropdown/checkbox options: filled by the server, never bound from the form
    [ValidateNever]
    public IEnumerable<SelectListItem> RoomTypeOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    [ValidateNever]
    public List<AmenityOption> AmenityOptions { get; set; } = new();
}

public record AmenityOption(int AmenityId, string Name);