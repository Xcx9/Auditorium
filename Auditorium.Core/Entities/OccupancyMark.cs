namespace Auditorium.Core.Entities;

public class OccupancyMark
{
    private int _id;
    private int _roomId;
    private int _userId;
    private DateTimeOffset _createdAt;
    private DateTimeOffset? _closedAt;
    private DateTimeOffset? _autoReleaseAt;

    private OccupancyMark() { } // EF Core

    public OccupancyMark(int roomId, int userId, DateTimeOffset createdAt)
    {
        _roomId = roomId;
        _userId = userId;
        _createdAt = createdAt;
    }

    public int Id => _id;
    public int RoomId => _roomId;
    public int UserId => _userId;
    public DateTimeOffset CreatedAt => _createdAt;
    public DateTimeOffset? ClosedAt => _closedAt;
    public DateTimeOffset? AutoReleaseAt => _autoReleaseAt;
    public Room Room { get; private set; } = null!;
    public User User { get; private set; } = null!;

    // Начало следующего занятия; null, если следующего занятия нет.
    public void SetAutoReleaseAt(DateTimeOffset? autoReleaseAt)
    {
        if (autoReleaseAt.HasValue && autoReleaseAt.Value <= CreatedAt)
            throw new ArgumentException("Время автоснятия должно быть позже создания отметки.", nameof(autoReleaseAt));
        _autoReleaseAt = autoReleaseAt;
    }

    // Время передаёт сервис из IClock либо из времени наступившего автоснятия.
    public void ApplySchedule(DateTimeOffset? nextLesson, bool coversPreviousDeadline, DateTimeOffset now)
    {
        if (ClosedAt.HasValue)
            return;

        if (AutoReleaseAt is { } known)
        {
            if (known <= now)
            {
                Close(known);
                return;
            }

            if (!coversPreviousDeadline && (!nextLesson.HasValue || known < nextLesson.Value))
                nextLesson = known;
        }

        SetAutoReleaseAt(nextLesson);
        if (nextLesson is { } due && due <= now)
            Close(due);
    }

    // Закрытие хранит момент события, даже если обработчик вызван позже.
    public void Close(DateTimeOffset closedAt)
    {
        if (ClosedAt.HasValue) return;
        if (closedAt < CreatedAt)
            throw new ArgumentException("Закрытие не может предшествовать созданию отметки.", nameof(closedAt));
        _closedAt = closedAt;
    }
}
