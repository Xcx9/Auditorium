using Auditorium.Core.Services;

namespace Auditorium.Core.Entities;

public class Room 
{
    private int _room_id;
    private string _number = string.Empty;
    private RoomState _status = RoomState.Free;

    private Room() { } // EF Core

    public Room(string number)
    {
        if (!IsValidNumber(number))
            throw new ArgumentException("Wrong type room id.", nameof(number));

        _number = number;
    }

    public int Id => _room_id;
    public string Number => _number;
    public int? SourceScheduleCode { get; private set; }
    public RoomState Status => _status;

    // Только даты с подтверждённым покрытием, включая дни без занятий.
    public List<DateOnly> ScheduleDates { get; private set; } = new();

    public void ApplySchedule(int sourceCode, string number, IEnumerable<DateOnly> dates)
    {
        if (sourceCode <= 0 || SourceScheduleCode is { } current && current != sourceCode)
            throw new ArgumentException("Код аудитории должен быть положительным и постоянным.", nameof(sourceCode));
        if (!IsValidNumber(number) || number.Length > 20)
            throw new ArgumentException("Некорректный номер аудитории.", nameof(number));

        SourceScheduleCode = sourceCode;
        _number = number;
        ScheduleDates = dates.Distinct().Order().ToList();
    }
    
    public static bool IsValidNumber(string number)
    {
        return !string.IsNullOrWhiteSpace(number);
    }

    public string FullName() => Number;
    
    public List<DateOnly> GetScheduleDates(){return ScheduleDates;}

    public int BuildingNumber()
    {
        return int.TryParse(Number[..1], out int result) ? result : 0;
    }

    public int FloorNumber()
    {
        return Number.Length >= 2 &&
               int.TryParse(Number.Substring(1, 1), out int result)
            ? result
            : 0;
    }
    
    public void SetStatus(RoomState status){_status = status;}
}
