using System;

namespace Auditorium.Core.DTOs;

public class TimeSlot
{
    public string? Time { get; set; }

    public int Code { get; set; }

    // Исходные значения внешнего API без смещения. При создании Lesson
    // парсер должен явно интерпретировать их в Europe/Moscow.
    public DateTime? TimeFrom { get; set; }

    public DateTime? TimeTo { get; set; }
}