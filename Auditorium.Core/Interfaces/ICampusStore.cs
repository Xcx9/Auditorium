using Auditorium.Core.Entities;

namespace Auditorium.Core.Interfaces;

public interface ICampusStore
{ 
        Task<List<Room>> GetAllRoomsAsync();
        Task<Room?> GetRoomByIdAsync(int id);
        Task<Room?> CheckRoomExistance(int? roomId);

        Task<List<OccupancyMark>> GetAllMarksAsync();
        Task<OccupancyMark?> GetActiveMarkByIdAsync(int userId);
        Task<List<OccupancyMark?>> GetDueMarksAsync(DateTimeOffset now);
        Task<OccupancyMark?> CheckMarkExistance(int markId);
        Task AddMark(OccupancyMark mark);
        Task RemoveMarkAsync(OccupancyMark mark);

        Task<User?> CheckUserExistance(int userId);
        // При необходимости добавьте другие методы:
        // Task<OccupancyMark?> GetByIdAsync(int id);
}