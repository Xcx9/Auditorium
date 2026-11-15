namespace Auditorium.Core.Entities;

public class ScheduleSnapshot
{
    public DateTimeOffset? LastSuccessfulUpdate { get; set; }
    public bool UpdateFailed { get; set; }

    public List<Lesson> Lessons { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
}