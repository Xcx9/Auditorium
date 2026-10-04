using System;

namespace Auditorium.Core.DTOs;

public class TimeSlot
{
    public string? Time { get; set; }

    public int Code { get; set; }

    public DateTime? TimeFrom { get; set; }

    public DateTime? TimeTo { get; set; }
}