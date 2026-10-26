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
    private DateTimeOffset? _autoReleaseAt = null;
    
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
    public DateTimeOffset? AutoReleaseAt => _autoReleaseAt;

    // Передаётся начало ближайшего занятия после создания отметки.
    // null означает, что следующего занятия нет.
    public void SetAutoReleaseAt(DateTimeOffset? autoReleaseAt)
    {
        if (autoReleaseAt.HasValue && autoReleaseAt.Value <= new DateTimeOffset(_createdAt))
            throw new ArgumentException(
                "Время автоснятия должно быть позже создания отметки.",
                nameof(autoReleaseAt));

        _autoReleaseAt = autoReleaseAt;
    }

    public Room Room { get; private set; } = null!;
    public User User { get; private set; } = null!;

    // Тут же нужно убирать из БД саму метку
    public void Close(Room room)
    {
        room.SetStatus(RoomState.Free);
        _closedAt = DateTimeOffset.Now;
        
    }
}