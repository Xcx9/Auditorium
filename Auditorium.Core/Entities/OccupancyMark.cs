using Auditorium.Core.DTOs;
using Auditorium.Core.Services;

namespace Auditorium.Core.Entities;

public class OccupancyMark
{
    private int _id;
    private int _roomId;
    private int _userId;
    private DateTime _createdAt;
    private DateTimeOffset? _closedAt = null;
    

    public OccupancyMark(int roomId, int userId, DateTime createdAt)
    {
        _id += 1;
        _roomId = roomId;
        _userId = userId;
        _createdAt = createdAt;
    }

    public int Id => _id;
    public int RoomId => _roomId;
    public int UserId => _userId;
    public DateTime CreatedAt => _createdAt;
    public DateTimeOffset? ClosedAt => _closedAt;

    public Room Room { get; private set; } = null!;
    public User User { get; private set; } = null!;

    // Тут же нужно убирать из БД метку с id
    public void Close(Room room)
    {
        _closedAt = DateTimeOffset.Now;
        room.SetStatus(RoomState.Free);
    }
}