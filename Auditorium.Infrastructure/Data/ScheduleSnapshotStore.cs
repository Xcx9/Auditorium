using Auditorium.Core.DTOs;
using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Auditorium.Infrastructure.Data;

public sealed class ScheduleSnapshotStore(IDbContextFactory<AppDbContext> contextFactory, IClock clock)
    : IScheduleSnapshotStore
{
    public async Task<ScheduleSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        // Read all parts of the same snapshot without reserving SQLite's write lock.
        await using var transaction = connection.BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);

        var state = await db.ScheduleImportStates.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var rooms = await db.Rooms.ToListAsync(cancellationToken);
        var lessons = await db.ScheduleEntries.ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new ScheduleSnapshot
        {
            Rooms = rooms,
            Lessons = lessons,
            LastSuccessfulUpdate = state?.LastSuccessfulUpdate,
            UpdateFailed = state?.UpdateFailed ?? false
        };
    }

    public async Task<ScheduleSnapshotState> SaveAsync(
        ScheduleLoadResult.Loaded candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var rooms = await db.Rooms.ToListAsync(cancellationToken);
            var roomsByCode = rooms.Where(room => room.SourceScheduleCode.HasValue)
                .ToDictionary(room => room.SourceScheduleCode!.Value);
            var incomingCodes = candidate.Rooms.Select(item => item.SourceRoomCode).ToHashSet();
            foreach (var room in rooms)
            {
                if (room.SourceScheduleCode is not { } code || !incomingCodes.Contains(code))
                    room.ScheduleDates.Clear();
            }

            var savedSchedules = new List<(Room Room, IReadOnlyList<Lesson> Lessons)>();
            foreach (var item in candidate.Rooms)
            {
                if (!roomsByCode.TryGetValue(item.SourceRoomCode, out var room))
                {
                    room = new Room(item.Room.Number);
                    db.Rooms.Add(room);
                }

                room.ApplySchedule(item.SourceRoomCode, item.Room.Number, item.Room.ScheduleDates);
                savedSchedules.Add((room, item.Lessons));
            }

            await db.ScheduleEntries.ExecuteDeleteAsync(cancellationToken);
            var lessons = savedSchedules.SelectMany(schedule => schedule.Lessons
                .Select(lesson => new Lesson(schedule.Room, lesson.StartsAt, lesson.EndsAt)));
            db.ScheduleEntries.AddRange(lessons);

            var now = clock.UtcNow;
            await RecalculateMarksAsync(db, savedSchedules, now, cancellationToken);
            var state = await GetOrAddStateAsync(db, cancellationToken);
            state.LastSuccessfulUpdate = now;
            state.UpdateFailed = false;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ScheduleSnapshotState(state.LastSuccessfulUpdate, state.UpdateFailed);
        }
        catch (Exception error) when (error is DbUpdateException or SqliteException)
        {
            throw new ScheduleStorageException("Не удалось сохранить расписание в SQLite.", error);
        }
    }

    public async Task<ScheduleSnapshotState> RecordFailureAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var state = await GetOrAddStateAsync(db, cancellationToken);
            state.UpdateFailed = true;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ScheduleSnapshotState(state.LastSuccessfulUpdate, state.UpdateFailed);
        }
        catch (Exception error) when (error is DbUpdateException or SqliteException)
        {
            throw new ScheduleStorageException("Не удалось сохранить признак сбоя обновления.", error);
        }
    }

    private async Task RecalculateMarksAsync(
        AppDbContext db, List<(Room Room, IReadOnlyList<Lesson> Lessons)> schedules, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var byRoomId = schedules.Where(schedule => schedule.Room.Id > 0)
            .ToDictionary(schedule => schedule.Room.Id);
        var activeMarks = await db.OccupancyMarks.Where(mark => mark.ClosedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var mark in activeMarks)
        {
            if (byRoomId.TryGetValue(mark.RoomId, out var schedule))
            {
                var next = schedule.Lessons.Where(lesson => lesson.StartsAt > mark.CreatedAt)
                    .Select(lesson => (DateTimeOffset?)lesson.StartsAt).Min();
                var coversDeadline = mark.AutoReleaseAt is { } deadline &&
                    schedule.Room.ScheduleDates.Contains(clock.GetDate(deadline));
                mark.ApplySchedule(next, coversDeadline, now);
            }
            else
            {
                mark.ApplySchedule(null, false, now);
            }
        }
    }

    private static async Task<ScheduleImportState> GetOrAddStateAsync(
        AppDbContext db, CancellationToken cancellationToken)
    {
        var state = await db.ScheduleImportStates.SingleOrDefaultAsync(cancellationToken);
        if (state is not null)
            return state;

        state = new ScheduleImportState();
        db.ScheduleImportStates.Add(state);
        return state;
    }
}
