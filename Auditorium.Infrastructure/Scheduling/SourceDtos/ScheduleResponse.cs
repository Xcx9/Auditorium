namespace Auditorium.Infrastructure.Scheduling.SourceDtos;

internal sealed record RoomInfo(int Code, string? Name);

internal sealed record ScheduleResponse(
    string? Semestr, List<TimeSlot?>? Times, List<ScheduleItem?>? Data);

internal sealed record ScheduleItem(int? Day, int? DayNumber, TimeSlot? Time, RoomInfo? Room);

internal sealed record TimeSlot(int Code, DateTime? TimeFrom, DateTime? TimeTo);
