using System.Collections.Generic;

namespace Auditorium.Core.DTOs;

public class ScheduleResponse
{
    public List<TimeSlot>? Times { get; set; }

    public List<ScheduleItem>? Data { get; set; }
}