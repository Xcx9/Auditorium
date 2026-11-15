using Auditorium.Core.Entities;

namespace Auditorium.Core.Interfaces;

public interface ICampusStore
{
    Task<List<Room>> GetAllRoomsAsync();
    Task<List<int>> GetOccupiedRoomIdsAsync(DateTimeOffset now);
    Task<Room?> GetRoomByIdAsync(int id);
    Task<Room?> CheckRoomExistance(int? roomId);
    Task<bool> HasActiveLessonAsync(int roomId, DateTimeOffset now);
    Task<DateTimeOffset?> GetNextLessonStartAsync(int roomId, DateTimeOffset now);
    Task<bool> HasActiveMarkForRoomAsync(int roomId);
    Task<List<OccupancyMark>> GetAllMarksAsync();
    Task<OccupancyMark?> GetActiveMarkByIdAsync(int userId);
    Task<int> CloseDueMarksAsync(DateTimeOffset now);
    Task<OccupancyMark?> CheckMarkExistance(int markId);
    Task<bool> AddMark(OccupancyMark mark);
    Task<bool> RemoveMarkAsync(int userId, int markId, DateTimeOffset closedAt);

    Task SaveChangesAsync();
    Task<User?> CheckUserExistance(int userId);
}
