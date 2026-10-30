using HotelBookingSystem.ViewModels;

namespace HotelBookingSystem.Services;

public interface IRoomService
{
    Task<List<RoomListItemViewModel>> GetAllRoomsAsync();
    Task<RoomDetailsViewModel?> GetRoomDetailsAsync(int roomId);
    Task<RoomFormViewModel?> GetRoomForEditAsync(int roomId);
    Task PopulateFormOptionsAsync(RoomFormViewModel model);
    Task<ServiceResult> CreateRoomAsync(RoomFormViewModel model);
    Task<ServiceResult> UpdateRoomAsync(RoomFormViewModel model);
    Task<ServiceResult> SetActiveAsync(int roomId, bool isActive);
}