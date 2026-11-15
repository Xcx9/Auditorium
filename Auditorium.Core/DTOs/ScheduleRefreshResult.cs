namespace Auditorium.Core.DTOs;

public sealed record ScheduleSnapshotState(DateTimeOffset? LastSuccessfulUpdate, bool UpdateFailed);

public sealed record ScheduleRefreshResult(ScheduleSnapshotState? State, string? Error)
{
    public bool Succeeded => Error is null;
}
