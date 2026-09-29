using System;

namespace Auditorium.Core.DTOs
{
    public class TimeSlot
    {
        public string? Time { get; set; }        // "1 пара"
        public int Code { get; set; }            // 1, 2, 3...
        public DateTime? TimeFrom { get; set; }
        public DateTime? TimeTo { get; set; }
    }
}