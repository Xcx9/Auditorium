namespace Auditorium.Infrastructure.Data;

public sealed class ScheduleImportState
{
    public int Id { get; private set; } = 1;
    public DateTimeOffset? LastSuccessfulUpdate { get; set; }
    public bool UpdateFailed { get; set; }
}
