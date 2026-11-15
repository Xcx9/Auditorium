using Auditorium.Core.DTOs;
using Auditorium.Core.Entities;
using Auditorium.Core.Interfaces;
using Auditorium.Core.Services;
using Auditorium.Infrastructure.Data;
using Auditorium.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace Auditorium.Tests;

public class ScheduleUpdaterTests
{
    private static readonly DateOnly Day = new(2026, 11, 8);
    private static readonly ScheduleLoadRequest Request = new("Тестовый семестр", Day, Day);
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 11, 0, 0, TimeSpan.FromHours(3));

    [Test]
    public async Task MatchesLegacyRoomByCodeAndKeepsMissingRoomAndHistory()
    {
        await using var database = await TestDatabase.CreateAsync(legacy: true);
        var updater = database.CreateUpdater(Load(Room(120, "1201 (м)", Now.AddHours(2)),
            Room(159, "УВЦ 1", Now.AddHours(3))));
        Assert.That((await updater.Refresh(Request)).Succeeded, Is.True);
        var markId = await database.AddMark(159, "owner", Now.AddHours(-1), Now.AddHours(3));

        updater = database.CreateUpdater(Load(Room(120, "1201 (м)", Now.AddHours(1)), Room(243, "8110")));
        Assert.That((await updater.Refresh(Request)).Succeeded, Is.True);
        Assert.That((await updater.Refresh(Request)).Succeeded, Is.True);
        var snapshot = await database.CreateStore().GetSnapshotAsync();

        Assert.That(snapshot.Rooms, Has.Count.EqualTo(3));
        var updated = snapshot.Rooms.Single(room => room.SourceScheduleCode == 120);
        Assert.That(updated.Id, Is.EqualTo(120));
        Assert.That(updated.Number, Is.EqualTo("1201 (м)"));
        Assert.That(snapshot.Rooms.Single(room => room.Id == 159).ScheduleDates, Is.Empty);
        Assert.That(snapshot.Rooms.Single(room => room.SourceScheduleCode == 243).ScheduleDates, Is.EqualTo(new[] { Day }));
        Assert.That(snapshot.Lessons.Single().RoomId, Is.EqualTo(120));
        Assert.That(snapshot.Lessons.Single().StartsAt, Is.EqualTo(Now.AddHours(1)));
        Assert.That(snapshot.LastSuccessfulUpdate, Is.EqualTo(Now));
        await using var db = database.CreateDbContext();
        var mark = await db.OccupancyMarks.SingleAsync();
        Assert.That(mark.Id, Is.EqualTo(markId));
        Assert.That(mark.RoomId, Is.EqualTo(159));
        Assert.That(mark.AutoReleaseAt, Is.EqualTo(Now.AddHours(3)));
    }

    [Test]
    public async Task RecordsFirstFailureAndRetainsPriorSnapshotOnLaterFailure()
    {
        await using var database = await TestDatabase.CreateAsync();
        var failure = database.CreateUpdater(new ScheduleLoadResult.Failed(ScheduleLoadError.SourceUnavailable, "Ошибка загрузки"));
        var first = await failure.Refresh(Request);
        Assert.That(first.Succeeded, Is.False);
        Assert.That(first.State, Is.EqualTo(new ScheduleSnapshotState(null, true)));

        var success = await database.CreateUpdater(Load(Room(120, "1201", Now.AddHours(1)))).Refresh(Request);
        Assert.That(success.State, Is.EqualTo(new ScheduleSnapshotState(Now, false)));
        var second = await failure.Refresh(Request);
        Assert.That(second.State, Is.EqualTo(new ScheduleSnapshotState(Now, true)));

        var snapshot = await database.CreateStore().GetSnapshotAsync();
        Assert.That(snapshot.UpdateFailed, Is.True);
        Assert.That(snapshot.LastSuccessfulUpdate, Is.EqualTo(Now));
        Assert.That(snapshot.Lessons.Single().StartsAt, Is.EqualTo(Now.AddHours(1)));
    }

    [Test]
    public async Task RollsBackReplacementIfSqliteRejectsAnInsert()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUpdater(Load(Room(120, "1201", Now.AddHours(1)))).Refresh(Request);
        await using (var db = database.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER FailInsert BEFORE INSERT ON ScheduleEntries
                BEGIN SELECT RAISE(ABORT, 'fixture insert failure'); END;
                """);
        }

        var failed = await database.CreateUpdater(Load(Room(120, "переименована", Now.AddHours(2)), Room(243, "8110")))
            .Refresh(Request);
        Assert.That(failed.Succeeded, Is.False);
        var snapshot = await database.CreateStore().GetSnapshotAsync();
        Assert.That(snapshot.UpdateFailed, Is.True);
        Assert.That(snapshot.LastSuccessfulUpdate, Is.EqualTo(Now));
        Assert.That(snapshot.Rooms.Single().Number, Is.EqualTo("1201"));
        Assert.That(snapshot.Lessons.Single().StartsAt, Is.EqualTo(Now.AddHours(1)));
    }

    [Test]
    public async Task RecalculatesUpcomingDeadlineAndClosesDueMarksAtLessonStart()
    {
        await using var database = await TestDatabase.CreateAsync(legacy: true);
        var previousDeadline = Now.AddHours(3);
        await database.AddMark(120, "first", Now.AddHours(-2), previousDeadline);
        await database.AddMark(159, "second", Now.AddHours(-3), Now.AddHours(-1));
        var result = await database.CreateUpdater(Load(Room(120, "1201", Now.AddHours(1)), Room(159, "УВЦ 1")))
            .Refresh(Request);
        Assert.That(result.Succeeded, Is.True);

        await using var db = database.CreateDbContext();
        var marks = await db.OccupancyMarks.ToDictionaryAsync(mark => mark.RoomId);
        Assert.That(marks[120].AutoReleaseAt, Is.EqualTo(Now.AddHours(1)));
        Assert.That(marks[120].ClosedAt, Is.Null);
        Assert.That(marks[159].ClosedAt, Is.EqualTo(Now.AddHours(-1)));
    }

    private static RoomSchedule Room(int code, string number, params DateTimeOffset[] starts)
    {
        var room = new Room(number);
        room.ScheduleDates.Add(Day);
        return new RoomSchedule(code, room, starts.Select(start => new Lesson(room, start, start.AddHours(1))).ToList());
    }

    private static ScheduleLoadResult.Loaded Load(params RoomSchedule[] rooms) => new(rooms);

    private sealed class StubParser(ScheduleLoadResult result) : IScheduleParser
    {
        public Task<ScheduleLoadResult> LoadAndValidate(ScheduleLoadRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }

    private sealed class TestDatabase(SqliteConnection anchor, DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>, IAsyncDisposable
    {
        public AppDbContext CreateDbContext() => new(options);
        public ValueTask DisposeAsync() => anchor.DisposeAsync();
        public ScheduleSnapshotStore CreateStore() => new(this, new MoscowClock(new FixedTimeProvider()));
        public ScheduleUpdater CreateUpdater(ScheduleLoadResult result) => new(new StubParser(result), CreateStore());

        public static async Task<TestDatabase> CreateAsync(bool legacy = false)
        {
            var connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared;Pooling=False";
            var anchor = new SqliteConnection(connectionString);
            await anchor.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
            var database = new TestDatabase(anchor, options);
            await using var db = database.CreateDbContext();
            if (legacy)
            {
                await db.GetService<IMigrator>().MigrateAsync("20260930130609_AddAudiencesTable");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO Audiences (Id, Name) VALUES (120, '1201 м'), (159, 'УВЦ 1')");
            }
            await db.Database.MigrateAsync();
            return database;
        }

        public async Task<int> AddMark(int roomId, string login, DateTimeOffset createdAt, DateTimeOffset due)
        {
            await using var db = CreateDbContext();
            var user = new User(login, "fixture");
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var mark = new OccupancyMark(roomId, user.Id, createdAt);
            mark.SetAutoReleaseAt(due);
            db.OccupancyMarks.Add(mark);
            await db.SaveChangesAsync();
            return mark.Id;
        }
    }
}
