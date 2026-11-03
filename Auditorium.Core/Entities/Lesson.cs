namespace Auditorium.Core.Entities;

public class Lesson
{
    private int _id;
    private int _roomId;
    private DateTimeOffset _startsAt;
    private DateTimeOffset _endsAt;

    private Lesson() { } // EF Core

    public Lesson(int roomId, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (endsAt <= startsAt)
            throw new ArgumentException("Конец занятия должен быть позже начала.", nameof(endsAt));

        _roomId = roomId;
        _startsAt = startsAt;
        _endsAt = endsAt;
    }

    public int Id => _id;
    public int RoomId => _roomId;
    public DateTimeOffset StartsAt => _startsAt;
    public DateTimeOffset EndsAt => _endsAt;
    public Room Room { get; private set; } = null!;

    public bool IsActiveAt(DateTimeOffset moment) => moment >= StartsAt && moment < EndsAt;
    public DateTimeOffset? NextStartAfter(DateTimeOffset moment) => StartsAt > moment ? StartsAt : null;
}
