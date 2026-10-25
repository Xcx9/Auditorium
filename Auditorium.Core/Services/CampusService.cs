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
    public RoomState State { get; }

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

    private async Task<CampusOperationResult?> IsUserExists(int userId)
    {
        if (await _store.CheckUserExistance(userId) == null)
            return new CampusOperationResult(
                CampusOperationCode.UserNotFound,
                "Пользователь не найден");
        return null;
    }

    private async Task<CampusOperationResult?> IsRoomExist(int roomId)
    {
        if (await _store.CheckRoomExistance(roomId) == null)
            return new CampusOperationResult(
                CampusOperationCode.RoomNotFound,
                "Комната не найдена");
        return null;
    }

    private async Task<CampusOperationResult?> IsMarkExist(int markId)
    {
        if (await _store.CheckMarkExistance(markId) == null)
            return new CampusOperationResult(
                CampusOperationCode.MarkNotFound,
                "Метки не существует.");
        return null;
    }
    
    private CampusOperationResult? IsRoomFree(Room room)
    {
        if (room.Status == RoomState.Busy)
            return new CampusOperationResult(
                CampusOperationCode.RoomAlreadyOccupied,
                "Аудитория уже занята другим пользователем.");
        return null;
    }
    
    public RoomState GetStatus(Room room, DateTime now)
    {
        // Пока возвращаем сохранённое состояние.
        // Позже здесь нужно учитывать расписание
        // и активные отметки.
        return room.Status;
    }
    
    /// <summary>
    /// Метод для получения информации о свободных аудиториях
    /// </summary>
    /// <param name="building"></param>
    /// <param name="now"></param>
    /// <returns></returns>
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

    
    //TODO: Нужно где-то выводить случай не найденной комнаты
    /// <summary>
    /// Метод для получения информации о конкретной аудитории
    /// </summary>
    /// <param name="roomId"></param>
    /// <param name="now"></param>
    /// <returns>RoomStatusInfo</returns>
    public async Task<RoomStatusInfo?> GetRoom(int roomId, DateTime now)
    {
        var room = await _store.GetRoomByIdAsync(roomId);
        if (room == null) return null;
        var state = GetStatus(room, now);
        
        return new RoomStatusInfo(room, state);
    }
    
    /// <summary>
    /// Метод для получения информации о активной метке пользователя
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>ampusOperationResult</returns>
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
    
    /// <summary>
    /// Метод для создания отметки занятости аудитории
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="roomId"></param>
    /// <returns>ampusOperationResult</returns>
    public async Task<CampusOperationResult> CreateMark(int userId, int roomId)
    {
        // TODO: Проверка на то что аудитория свободна, что пользователь существует
        OccupancyMark mark = new OccupancyMark(roomId, userId, DateTime.Now);

        var error = await IsUserExists(userId);
        if (error != null) return error;
        
        error = await IsRoomExist(roomId);
        if (error != null) return error;
        IsRoomFree(mark.Room);
        mark.Room.SetStatus(RoomState.Busy);
        
        return new CampusOperationResult(
            CampusOperationCode.Success,
            "Аудитория занята.");
    }
    
    /// <summary>
    /// Метод для отмены активной метки пользователя
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>CampusOperationalResult</returns>
    public async Task<CampusOperationResult> CancelMark(int userId)
    {
        var mark = await _store.GetActiveMarkByIdAsync(userId);
        if  (mark == null)
            return new CampusOperationResult(
                CampusOperationCode.MarkNotFound,
                "Активная метка не найдена.");
        
        var error = await IsUserExists(userId);
        if (error != null) return error;
        error = await IsRoomExist(mark.RoomId);
        if (error != null) return error;
        if (GetStatus(mark.Room, DateTime.Now) == RoomState.Busy)
            return new CampusOperationResult(
                CampusOperationCode.RoomAlreadyOccupied,
                "Комната уже занята");
        
        mark.Close(mark.Room);
        return new CampusOperationResult(
            CampusOperationCode.Success,
            "Метка снята.");
    }
    
    /// <summary>
    /// Метод для?
    /// </summary>
    /// <param name="now"></param>
    public void ReleaseDueMarks(DateTime now)
    {
        
    }
}
