using Auditorium.Core.Services;

namespace Auditorium.Core.Entities;

public class Room 
{
    private int _room_id;
    private string _number = string.Empty;
    public string Building =>
        BuildingNumber() is var number && number > 0
            ? number.ToString()
            : string.Empty;

    private Room() { } // EF Core

    public Room(string number)
    {
        if (!IsValidNumber(number))
            throw new ArgumentException("Некорректный номер аудитории.", nameof(number));

        _number = number.Trim();
    }

    public int Id => _room_id;
    public string Number => _number;
    public int? SourceScheduleCode { get; private set; }

    // Только даты с подтверждённым покрытием, включая дни без занятий.
    public List<DateOnly> ScheduleDates { get; private set; } = new();

    public RoomState? Status(DateOnly date, bool isOccupied)
    {
        if (!ScheduleDates.Contains(date))
            return null;

        return isOccupied ? RoomState.Busy : RoomState.Free;
    }

    public void ApplySchedule(int sourceCode, string number, IEnumerable<DateOnly> dates)
    {
        if (sourceCode <= 0 || SourceScheduleCode is { } current && current != sourceCode)
            throw new ArgumentException("Код аудитории должен быть положительным и постоянным.", nameof(sourceCode));
        if (!IsValidNumber(number))
            throw new ArgumentException("Некорректный номер аудитории.", nameof(number));
        SourceScheduleCode = sourceCode;
        _number = number.Trim();
        ScheduleDates = dates.Distinct().Order().ToList();
    }
    
    public static bool IsValidNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number) || number.Any(char.IsControl))
            return false;

        var value = number.Trim();
        return value.Length is >= 4 and <= 20
            && value[0] is >= '1' and <= '9'
            && value.Take(4).All(char.IsAsciiDigit)
            && value.Skip(4).All(character => char.IsLetter(character)
                || character is ' ' or '(' or ')' or '-');
    }

    public string FullName() => Number;
    
    public List<DateOnly> GetScheduleDates(){return ScheduleDates;}

    public int BuildingNumber()
    {
        return Number.Length > 0 && Number[0] is >= '1' and <= '9'
            ? Number[0] - '0'
            : 0;
    }

    public int FloorNumber()
    {
        return BuildingNumber() != 0 && Number.Length >= 2 && char.IsAsciiDigit(Number[1])
            ? Number[1] - '0'
            : 0;
    }
}
