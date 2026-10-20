
using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;

namespace Auditorium.Core.Services;

public enum RoomState
{
    Free,
    Busy
}

public enum MarkResult
{
    Success,
    AlreadyBusyByUser,
    AlreadyBusyByRoom,
    NotOwner,
    ClassInProgress,
    NotFound
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

public class RoomStatusService
{
    private readonly IRoomRepository _rooms;

    public RoomStatusService(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public RoomState GetStatus(Room room, DateTime now)
    {
        // Пока возвращаем сохранённое состояние.
        // Позже здесь нужно учитывать расписание
        // и активные отметки.
        return room.State;
    }

    public async Task<List<RoomStatusInfo>> GetFreeRoomsAsync(
        DateTime now)
    {
        var rooms = await _rooms.GetAllAsync();
        var result = new List<RoomStatusInfo>();

        foreach (var room in rooms)
        {
            var state = GetStatus(room, now);

            if (state == RoomState.Free)
            {
                result.Add(new RoomStatusInfo(room, state));
            }
        }

        return result;
    }
}
