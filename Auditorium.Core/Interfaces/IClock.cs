namespace Auditorium.Core.Interfaces;

/// <summary>Серверное время и календарная дата в Europe/Moscow.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now { get; }
    DateOnly Today { get; }
    DateOnly GetDate(DateTimeOffset instant);
}
