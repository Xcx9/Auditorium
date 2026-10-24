using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;

namespace Auditorium.Core.Services;

public enum RoomState
{
    Free,
    Busy
}

public sealed class RoomStatusInfo
{
    public Room Room { get; }
    public RoomState State { get; }

    public RoomStatusInfo(Room room, RoomState state)
    {
        Room = room;
        State = state;
    }
}

public class CampusService
{
    private readonly IRoomRepository _rooms;

    public CampusService(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public RoomState GetStatus(Room room, DateTime now)
    {
        // Пока возвращаем сохранённое состояние.
        // Позже здесь нужно учитывать расписание
        // и активные отметки.
        return room.Status;
    }
    

    public async Task<List<RoomStatusInfo>> FindFree(int building,
        DateTime now)
    {
        var rooms = await _rooms.GetAllAsync();
        var result = new List<RoomStatusInfo>();

        foreach (var room in rooms)
        {
            var state = GetStatus(room, now);

            if (state == RoomState.Free && room.BuildingNumber() == building)
            {
                result.Add(new RoomStatusInfo(room, state));
            }
        }

        return result;
    }

    public async Task<List<RoomStatusInfo>> GetRoom(string roomId, DateTime now)
    {
        var rooms = await _rooms.GetAllAsync();
        var result = new List<RoomStatusInfo>();

        foreach (var room in rooms)
        {
            var state = GetStatus(room, now);

            if (state == RoomState.Free && roomId == room.Number)
            {
                result.Add(new RoomStatusInfo(room, state));
                break;
            }
        }
        return result;
    }

    public void GetOwnMark(int userId)
    {
        
    }
    
    public void CreateMark(int userId)
    {
        
    }
    
    public void CancelMark(int userId)
    {
        
    }

    public void ReleaseDueMarks(DateTime now)
    {
        
    }
}
