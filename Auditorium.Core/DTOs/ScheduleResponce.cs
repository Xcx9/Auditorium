using System.Collections.Generic;

namespace Auditorium.Core.DTOs
{
    /// <summary>
    /// Корневой объект JSON-ответа от API расписания МИЭТ.
    /// </summary>
    public class ScheduleResponse
    {
        // Список всех временных слотов (пар) в течение дня.
        public List<TimeSlot>? Times { get; set; }

        // Список занятий.
        public List<ScheduleItem>? Data { get; set; }
    }
}