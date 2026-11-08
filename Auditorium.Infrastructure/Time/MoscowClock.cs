using Auditorium.Core.Interfaces;

namespace Auditorium.Infrastructure.Time;

public sealed class MoscowClock : IClock
{
    private static readonly TimeZoneInfo MoscowZone = FindMoscowTimeZone();
    private readonly TimeProvider _timeProvider;

    public MoscowClock() : this(TimeProvider.System) { }

    public MoscowClock(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(UtcNow, MoscowZone);
    public DateOnly Today => GetDate(UtcNow);

    public DateOnly GetDate(DateTimeOffset instant) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(instant, MoscowZone).DateTime);

    internal static DateTimeOffset AtLocalTime(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, MoscowZone.GetUtcOffset(local));
    }

    private static TimeZoneInfo FindMoscowTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        }
    }
}
