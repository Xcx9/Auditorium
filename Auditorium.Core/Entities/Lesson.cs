
namespace Auditorium.Core.Entities;

public class Lesson
{
    private int _id;
    private int _roomId;
    private DateTime _start;
    private DateTime _end;

    private Lesson() { } // EF Core

    public Lesson(int roomId, DateTime start, DateTime end)
    {
        if (end <= start)
            throw new ArgumentException(
                "End time should be greater than start time.");

        _roomId = roomId;
        _start = start;
        _end = end;
    }

    public int Id => _id;
    public int RoomId => _roomId;
    public DateTime Start => _start;
    public DateTime End => _end;

    public Room Room { get; private set; } = null!;

    public bool IsActiveAt(DateTime moment)
        => moment >= Start && moment < End;

    public DateTime? NextStartAfter(DateTime moment)
        => Start > moment ? Start : null;
}
