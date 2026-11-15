using Auditorium.Core.Entities;

namespace Auditorium.Core.DTOs;

/// <summary>Период из подтверждённого учебного календаря, обе даты включительно.</summary>
public sealed record ScheduleLoadRequest(string Semester, DateOnly From, DateOnly Through);

public sealed record RoomSchedule(int SourceRoomCode, Room Room, IReadOnlyList<Lesson> Lessons);

public enum ScheduleLoadError
{
    InvalidPeriod,
    SourceUnavailable,
    InvalidResponse
}

public abstract record ScheduleLoadResult
{
    public sealed record Loaded(IReadOnlyList<RoomSchedule> Rooms) : ScheduleLoadResult;
    public sealed record Failed(ScheduleLoadError Error, string Message) : ScheduleLoadResult;
}
