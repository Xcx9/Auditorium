using Auditorium.Core.DTOs;
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
    private readonly ICampusStore _store;

    public CampusService(ICampusStore store)
    {
        _store = store;
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
        var rooms = await _store.GetAllRoomsAsync();
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
        var rooms = await _store.GetAllRoomsAsync();
        var result = new List<RoomStatusInfo>();

        foreach (var room in rooms)
        {
            var state = GetStatus(room, now);

            if (roomId == room.Number)
            {
                result.Add(new RoomStatusInfo(room, state));
                break;
            }
        }
        return result;
    }

    public async Task<CampusOperationResult> GetOwnMark(int userId)
    {
        var mark = await _store.GetActiveMarkByIdAsync(userId);
        if (mark == null)
            return new CampusOperationResult(
                CampusOperationCode.MarkNotFound,
                "Отметка пользователя не найдена.");
        return new CampusOperationResult(
            CampusOperationCode.Success, "Ваша метка: " + mark.Room.Id + ", " + mark.Room.Status);
            
        
    }
    
    
    public async Task<CampusOperationResult> CreateMark(int userId, string roomId)
    {
        // TODO: Проверка на то что аудитория свободна, что пользователь существует
        var marks = await _store.GetActiveMarkByIdAsync(userId);
        OccupancyMark mark = new OccupancyMark(roomId, userId, DateTime.Now);
        mark.Room.SetStatus(RoomState.Busy);
        
        return new CampusOperationResult(
            CampusOperationCode.Success,
            "Аудитория занята.");
    }
    
    public async Task<CampusOperationResult> CancelMark(int userId)
    {
        var mark = await _store.GetActiveMarkByIdAsync(userId);
        if  (mark == null)
            return new CampusOperationResult(
                CampusOperationCode.MarkNotFound,
                "Метка не найдена.");
        
        if (await _store.CheckMarkExistance(mark.Id) == null)
            return new CampusOperationResult(
                CampusOperationCode.MarkNotFound,
                "Метки не существует.");
        
        if (await _store.CheckUserExistance(userId) == null)
            return new CampusOperationResult(
                CampusOperationCode.UserNotFound,
                "Пользователь не найден");
        
        if (await _store.CheckRoomExistance(int.Parse(mark.RoomId)) == null)
            return new CampusOperationResult(
                CampusOperationCode.RoomNotFound,
                "Комната не найдена");

        if (GetStatus(mark.Room, DateTime.Now) == RoomState.Busy)
            return new CampusOperationResult(
                CampusOperationCode.RoomAlreadyOccupied,
                "Комната уже занята");
        
        Room room = mark.Room;
        room.SetStatus(RoomState.Free);
        return new CampusOperationResult(
                CampusOperationCode.Success,
                "Метка снята.");
        
    }

    public void ReleaseDueMarks(DateTime now)
    {
        
    }
}
