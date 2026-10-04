namespace Auditorium.Core.DTOs;

public class ScheduleItem
{
    public int? Day { get; set; }

    public int? DayNumber { get; set; }

    public TimeSlot? Time { get; set; }

    public ClassInfo? Class { get; set; }

    public RoomInfo? Room { get; set; }
}