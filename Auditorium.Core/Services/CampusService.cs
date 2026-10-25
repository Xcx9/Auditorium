using System.ComponentModel.Design;
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
    public RoomState State { get; set; }

    public RoomStatusInfo(Room room, RoomState state)
    {
        Room = room;
        State = state;
    }
}

public class CampusService
{
    private readonly IRoomRepository _rooms;
    private readonly IMarkRepository _marks;

    public CampusService(IRoomRepository rooms, IMarkRepository marks)
    {
        _rooms = rooms;
        _marks = marks;
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

    public async Task<OccupancyMark> GetOwnMark(int userId)
    {
        var marks = await _marks.GetByIdAsync(userId);
        
        foreach (var mark in marks.Where(mark => mark.UserId == userId))
        {
            return mark;
        }
        throw new ArgumentException("Mark for user not found.", nameof(userId));
    }
    
    public async Task<string> CreateMark(int userId, string roomId)
    {
        OccupancyMark mark = new OccupancyMark(roomId, userId, DateTime.Now);
        mark.Room.SetStatus(RoomState.Busy);
        var marks = await _marks.GetByIdAsync(userId);
        
        marks.Add(mark);
        return "Success";
    }
    
    public async Task<string> CancelMark(int userId)
    {
        var marks = await _marks.GetByIdAsync(userId);
        foreach (var mark in marks)
        {
            if (mark.UserId == userId)
            {
                Room room = mark.Room;
                marks.Remove(mark);
                room.SetStatus(RoomState.Free);
                break;
            }
        }
        return "Success";
    }

    public void ReleaseDueMarks(DateTime now)
    {
        
    }
}
