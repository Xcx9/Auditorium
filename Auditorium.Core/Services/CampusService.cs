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
    private readonly IClock _clock;

    public CampusService(ICampusStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<RoomState> GetStatus(Room room)
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        return await GetStatusAt(room, now);
    }

    private async Task<RoomState> GetStatusAt(Room room, DateTimeOffset now)
    {
        if (!HasScheduleFor(room, now))
            throw new InvalidOperationException("Нет данных расписания на текущую московскую дату.");
        return await _store.HasActiveLessonAsync(room.Id, now)
            || await _store.HasActiveMarkForRoomAsync(room.Id)
            ? RoomState.Busy : RoomState.Free;
    }

    private bool HasScheduleFor(Room room, DateTimeOffset now)
        => room.ScheduleDates.Contains(_clock.GetDate(now));

    public Task<DateTimeOffset?> FindNearOfficialLesson(int roomId)
        => _store.GetNextLessonStartAsync(roomId, _clock.UtcNow);

    public async Task<List<RoomStatusInfo>> FindFree(int building)
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        var rooms = await _store.GetAllRoomsAsync();
        var result = new List<RoomStatusInfo>();
        foreach (var room in rooms)
        {
            if (room.BuildingNumber() != building || !HasScheduleFor(room, now)) continue;
            var status = await GetStatusAt(room, now);
            if (status == RoomState.Free) result.Add(new RoomStatusInfo(room, status));
        }
        return result;
    }

    public async Task<RoomStatusInfo?> GetRoom(int roomId)
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        var room = await _store.GetRoomByIdAsync(roomId);
        return room == null || !HasScheduleFor(room, now)
            ? null : new RoomStatusInfo(room, await GetStatusAt(room, now));
    }

    public async Task<CampusOperationResult> GetOwnMark(int userId)
    {
        await ReleaseDueMarksAt(_clock.UtcNow);
        var mark = await _store.GetActiveMarkByIdAsync(userId);
        return mark == null
            ? new(CampusOperationCode.MarkNotFound, "Активная отметка не найдена.")
            : new(CampusOperationCode.Success, $"Ваша аудитория: {mark.Room.Number}, занято.");
    }

    public async Task<CampusOperationResult> CreateMark(int userId, int roomId)
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        if (await _store.CheckUserExistance(userId) == null)
            return new(CampusOperationCode.UserNotFound, "Пользователь не найден.");
        var room = await _store.GetRoomByIdAsync(roomId);
        if (room == null) return new(CampusOperationCode.RoomNotFound, "Аудитория не найдена.");
        if (!HasScheduleFor(room, now))
            return new(CampusOperationCode.ScheduleNotCovered, "Нет данных расписания на текущую московскую дату.");
        if (await _store.GetActiveMarkByIdAsync(userId) != null)
            return new(CampusOperationCode.UserAlreadyHasActiveMark, "У пользователя уже есть активная отметка.");
        if (await GetStatusAt(room, now) == RoomState.Busy)
            return new(CampusOperationCode.RoomAlreadyOccupied, "Аудитория занята.");

        var mark = new OccupancyMark(roomId, userId, now);
        mark.SetAutoReleaseAt(await _store.GetNextLessonStartAsync(roomId, now));
        await _store.AddMark(mark);
        mark.Room.SetStatus(RoomState.Busy);
        return new(CampusOperationCode.Success, "Аудитория отмечена занятой.");
    }

    public async Task<CampusOperationResult> CancelMark(int userId)
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        if (await _store.CheckUserExistance(userId) == null)
            return new(CampusOperationCode.UserNotFound, "Пользователь не найден.");
        var mark = await _store.GetActiveMarkByIdAsync(userId);
        if (mark == null) return new(CampusOperationCode.MarkNotFound, "Активная отметка не найдена.");
        mark.Room.SetStatus(RoomState.Free);
        mark.Close(now);
        await _store.SaveChangesAsync();
        return new(CampusOperationCode.Success, "Отметка снята.");
    }

    public Task<CampusOperationResult?> ReleaseDueMarks() => ReleaseDueMarksAt(_clock.UtcNow);

    // Для планировщика: переданный момент должен быть получен из серверных часов.
    public Task<CampusOperationResult?> ReleaseDueMarks(DateTimeOffset now) => ReleaseDueMarksAt(now);

    private async Task<CampusOperationResult?> ReleaseDueMarksAt(DateTimeOffset now)
    {
        var marks = await _store.GetDueMarksAsync(now);
        foreach (var mark in marks)
        {
            if (mark.ClosedAt == null && mark.AutoReleaseAt is { } due && due <= now)
                mark.Close(due);
        }
        if (marks.Count != 0) await _store.SaveChangesAsync();
        return new(CampusOperationCode.Success, "Наступившие отметки закрыты.");
    }
}
