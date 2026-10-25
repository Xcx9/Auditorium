
namespace Auditorium.Core.Entities;

public class OccupancyMark
{
    private int _id;
    private string _roomId;
    private int _userId;
    private DateTime _createdAt;
    private DateTimeOffset? _closedAt = null;
    

    public OccupancyMark(string roomId, int userId, DateTime createdAt)
    {
        _roomId = roomId;
        _userId = userId;
        _createdAt = createdAt;
    }

    public int Id => _id;
    public string RoomId => _roomId;
    public int UserId => _userId;
    public DateTime CreatedAt => _createdAt;
    public DateTimeOffset? ClosedAt => _closedAt;

    public Room Room { get; private set; } = null!;
    public User User { get; private set; } = null!;
    
}