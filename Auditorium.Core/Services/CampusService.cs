using Auditorium.Core.DTOs;
using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;

namespace Auditorium.Core.Services;

public enum RoomState
{
    Free,
    Busy
}

public sealed record FreeRoomSearchResult(
    IReadOnlyList<RoomStatusInfo> Rooms,
    int MatchingRoomCount,
    bool HasScheduleCoverage,
    ScheduleSnapshotState ScheduleState);

public sealed record RoomDetailsResult(
    RoomStatusInfo RoomInfo,
    ScheduleSnapshotState ScheduleState);

public sealed class RoomStatusInfo
{
    public Room Room { get; }
    public RoomState? State { get; }

    public RoomStatusInfo(Room room, RoomState? state)
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

    public async Task<RoomState> GetStatusNow(Room room)
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        return await GetStatusAt(room, now)
            ?? throw new InvalidOperationException("Нет данных расписания на текущую московскую дату.");
    }

    private async Task<RoomState?> GetStatusAt(Room room, DateTimeOffset now)
    {
        var date = _clock.GetDate(now);
        var isOccupied = room.ScheduleDates.Contains(date)
            && (await _store.HasActiveLessonAsync(room.Id, now)
                || await _store.HasActiveMarkForRoomAsync(room.Id));
        return room.Status(date, isOccupied);
    }

    private bool HasScheduleFor(Room room, DateTimeOffset now)
        => room.ScheduleDates.Contains(_clock.GetDate(now));

    public Task<DateTimeOffset?> FindNearOfficialLesson(int roomId)
        => _store.GetNextLessonStartAsync(roomId, _clock.UtcNow);

    public async Task<List<RoomStatusInfo>> GetRooms()
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        var date = _clock.GetDate(now);
        var rooms = await _store.GetAllRoomsAsync();
        var occupiedRoomIds = (await _store.GetOccupiedRoomIdsAsync(now)).ToHashSet();
        return rooms.Select(room => new RoomStatusInfo(room, room.Status(date, occupiedRoomIds.Contains(room.Id))))
            .ToList();
    }

    public async Task<FreeRoomSearchResult> FindFree(
        int building, string? numberPart = null)
    {
        var numberFilter = numberPart?.Trim();
        var rooms = await GetRooms();

        var matchingRooms = rooms
            .Where(room => room.Room.BuildingNumber() == building
                           && (string.IsNullOrEmpty(numberFilter)
                               || room.Room.Number.Contains(
                                   numberFilter, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var freeRooms = matchingRooms
            .Where(room => room.State == RoomState.Free)
            .ToList();

        return new FreeRoomSearchResult(
            freeRooms,
            matchingRooms.Count,
            matchingRooms.Any(room => room.State.HasValue),
            await _store.GetScheduleStateAsync());
    }

    public async Task<RoomStatusInfo?> GetRoom(int roomId)
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        var room = await _store.GetRoomByIdAsync(roomId);
        return room == null
            ? null : new RoomStatusInfo(room, await GetStatusAt(room, now));
    }
    
    public async Task<OwnMarkDto?> GetOwnMark(int userId)
    {
        await ReleaseDueMarksAt(_clock.UtcNow);

        var mark = await _store.GetActiveMarkByIdAsync(userId);
        if (mark is null)
            return null;

        return new OwnMarkDto(
            mark.Id,
            mark.RoomId,
            mark.Room.Number,
            mark.CreatedAt,
            mark.AutoReleaseAt);
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
        bool result = await _store.AddMark(mark);
        if (!result)
            return new(CampusOperationCode.ConcurrentConflict, "Не удалось создать отметку. Повторите попытку.");
        return new(CampusOperationCode.Success, "Аудитория отмечена занятой.");
    }

    public async Task<CampusOperationResult> CancelMark(int userId, int markId)
    {
        var now = _clock.UtcNow;
        await ReleaseDueMarksAt(now);
        if (await _store.CheckUserExistance(userId) == null)
            return new(CampusOperationCode.UserNotFound, "Пользователь не найден.");
        var mark = await _store.CheckMarkExistance(markId);

        if (mark == null)
            return new(CampusOperationCode.MarkNotFound, "Отметка не найдена.");
        if (mark.UserId != userId)
            return new(CampusOperationCode.NotOwner, "Снять отметку может только её автор.");
        if (mark.ClosedAt != null)
            return new(CampusOperationCode.MarkAlreadyClosed, "Отметка уже закрыта.");
        if (!await _store.RemoveMarkAsync(userId, markId, now))
            return new(CampusOperationCode.MarkAlreadyClosed, "Отметка уже закрыта.");

        return new(CampusOperationCode.Success, "Отметка снята.");
    }

    public Task<CampusOperationResult?> ReleaseDueMarks() => ReleaseDueMarksAt(_clock.UtcNow);

    // Для планировщика: переданный момент должен быть получен из серверных часов.
    public Task<CampusOperationResult?> ReleaseDueMarks(DateTimeOffset now) => ReleaseDueMarksAt(now);

    private async Task<CampusOperationResult?> ReleaseDueMarksAt(DateTimeOffset now)
    {
        await _store.CloseDueMarksAsync(now);
        return new(CampusOperationCode.Success, "Наступившие отметки закрыты.");
    }
}
