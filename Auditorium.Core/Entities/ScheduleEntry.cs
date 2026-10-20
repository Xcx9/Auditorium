
namespace Auditorium.Core.Entities;

public class ScheduleEntry
{
    private int _id;
    private int _roomId;
    private DateTime _start;
    private DateTime _end;

    private ScheduleEntry() { } // Для EF Core

    public ScheduleEntry(int roomId, DateTime start, DateTime end)
    {
        if (end <= start)
            throw new ArgumentException(
                "Время окончания должно быть позже начала.");

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
