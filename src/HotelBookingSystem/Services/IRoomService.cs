using HotelBookingSystem.ViewModels;

namespace HotelBookingSystem.Services;

public interface IRoomService
{
    // Admin
    Task<List<RoomListItemViewModel>> GetAllRoomsAsync();
    Task<RoomDetailsViewModel?> GetRoomDetailsAsync(int roomId);
    Task<RoomFormViewModel?> GetRoomForEditAsync(int roomId);
    Task PopulateFormOptionsAsync(RoomFormViewModel model);
    Task<ServiceResult> CreateRoomAsync(RoomFormViewModel model);
    Task<ServiceResult> UpdateRoomAsync(RoomFormViewModel model);
    Task<ServiceResult> SetActiveAsync(int roomId, bool isActive);

    // Public browsing and search
    Task<List<RoomCardViewModel>> GetActiveRoomCardsAsync();
    Task<List<RoomCardViewModel>> SearchAvailableRoomsAsync(DateOnly checkIn, DateOnly checkOut, int guests);
    Task<bool> IsRoomAvailableAsync(int roomId, DateOnly checkIn, DateOnly checkOut);
}