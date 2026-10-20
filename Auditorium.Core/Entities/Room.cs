
using Auditorium.Core.Services;

namespace Auditorium.Core.Entities;

public class Room
{
    private int _id;
    private string _number = string.Empty;
    private bool _isEnabled = true;
    private RoomState _state = RoomState.Free;

    private Room() { } // Для EF Core

    public Room(string number)
    {
        if (!IsValidNumber(number))
            throw new ArgumentException("Некорректный номер аудитории.", nameof(number));

        _number = number;
    }

    public int Id => _id;
    public string Number => _number;
    public bool IsEnabled => _isEnabled;
    public RoomState State => _state;

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
    }

    public void SetState(RoomState state)
    {
        _state = state;
    }

    public static bool IsValidNumber(string number)
    {
        return !string.IsNullOrWhiteSpace(number);
    }

    public string FullName() => Number;

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
}
