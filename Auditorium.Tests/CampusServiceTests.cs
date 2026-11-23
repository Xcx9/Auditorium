using Auditorium.Core.Entities;
using Auditorium.Core.DTOs;
using Auditorium.Core.Interfaces;
using Auditorium.Core.Services;
using Auditorium.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Auditorium.Tests;

public class CampusServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 9, 30, 0, TimeSpan.FromHours(3));
    private static readonly DateOnly Today = new(2026, 11, 23);

    [TestCase(false)]
    [TestCase(true)]
    public async Task ConcurrentCreationKeepsOneActiveMark(bool sameUser)
    {
        await using var database = await Database.CreateAsync();
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim();
        Task<CampusOperationResult> Create(int userId, int roomId) => Task.Run(async () =>
        {
            await using var db = database.Open();
            ready.Signal();
            if (!start.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Concurrent requests did not start.");
            return await Service(db).CreateMark(userId, roomId);
        });

        var first = Create(database.UserId, database.FirstRoomId);
        var second = Create(sameUser ? database.UserId : database.OtherUserId,
            sameUser ? database.SecondRoomId : database.FirstRoomId);
        var bothReady = ready.Wait(TimeSpan.FromSeconds(10));
        start.Set();
        var results = await Task.WhenAll(first, second);
        Assert.That(bothReady, Is.True);
        Assert.That(results.Count(result => result.IsSuccess), Is.EqualTo(1));
        Assert.That(results.Single(result => !result.IsSuccess).Code,
            Is.AnyOf(CampusOperationCode.ConcurrentConflict,
                sameUser ? CampusOperationCode.UserAlreadyHasActiveMark : CampusOperationCode.RoomAlreadyOccupied));

        await using var verify = database.Open();
        Assert.That(await verify.OccupancyMarks.CountAsync(mark => mark.ClosedAt == null), Is.EqualTo(1));
    }

    [Test]
    public async Task SearchAndDetailsDistinguishPartialCoverageAndMissingRoom()
    {
        await using var database = await Database.CreateAsync();
        await using var db = database.Open();
        db.ScheduleEntries.Add(new Lesson(database.SecondRoomId, Now, Now.AddHours(1)));
        db.ScheduleImportStates.Add(new ScheduleImportState { LastSuccessfulUpdate = Now.AddDays(-1), UpdateFailed = true });
        await db.SaveChangesAsync();
        var service = Service(db);

        var result = await service.FindFree(3, " 31 ");
        Assert.That(result.MatchingRoomCount, Is.EqualTo(3));
        Assert.That(result.RoomsWithoutScheduleCount, Is.EqualTo(1));
        Assert.That(result.HasScheduleCoverage, Is.True);
        Assert.That(result.Rooms.Select(room => room.Room.Id), Is.EqualTo(new[] { database.FirstRoomId }));
        Assert.That(result.ScheduleState.UpdateFailed, Is.True);
        Assert.That(result.ScheduleState.LastSuccessfulUpdate, Is.EqualTo(Now.AddDays(-1)));
        Assert.That(await service.GetRooms(), Has.Count.EqualTo(3));
        var uncovered = await service.GetRoom(database.UncoveredRoomId);
        Assert.That(uncovered, Is.Not.Null);
        Assert.That(uncovered!.RoomInfo.State, Is.Null);
        Assert.That(uncovered.ScheduleState.UpdateFailed, Is.True);
        Assert.That(await service.GetRoom(int.MaxValue), Is.Null);

        var absent = await service.FindFree(9);
        Assert.That(absent.MatchingRoomCount, Is.Zero);
        Assert.That(absent.HasScheduleCoverage, Is.False);
        var unavailable = await service.FindFree(3, "3103");
        Assert.That(unavailable.RoomsWithoutScheduleCount, Is.EqualTo(1));
        Assert.That(unavailable.HasScheduleCoverage, Is.False);
    }

    [Test]
    public async Task CancellationTargetsShownMarkAndPreservesHistory()
    {
        await using var database = await Database.CreateAsync();
        await using var db = database.Open();
        var service = Service(db);
        Assert.That((await service.CreateMark(database.UserId, database.FirstRoomId)).IsSuccess, Is.True);
        var own = await service.GetOwnMark(database.UserId);
        Assert.That(own!.RoomId, Is.EqualTo(database.FirstRoomId));
        Assert.That(own.AutoReleaseAt, Is.Null);
        Assert.That((await service.CancelMark(database.OtherUserId, own.MarkId)).Code, Is.EqualTo(CampusOperationCode.NotOwner));
        Assert.That((await service.CancelMark(database.UserId, own.MarkId)).IsSuccess, Is.True);
        Assert.That(await service.GetOwnMark(database.UserId), Is.Null);
        Assert.That((await service.CreateMark(database.UserId, database.SecondRoomId)).IsSuccess, Is.True);
        Assert.That((await service.CancelMark(database.UserId, own.MarkId)).Code, Is.EqualTo(CampusOperationCode.MarkAlreadyClosed));
        Assert.That((await service.GetOwnMark(database.UserId))!.RoomId, Is.EqualTo(database.SecondRoomId));

        await using var verify = database.Open();
        Assert.That(await verify.OccupancyMarks.CountAsync(), Is.EqualTo(2));
        Assert.That((await verify.OccupancyMarks.SingleAsync(mark => mark.Id == own.MarkId)).ClosedAt, Is.EqualTo(Now));
    }

    [Test]
    public async Task ManualAndAutomaticCloseCannotOverwriteEachOther()
    {
        await using var database = await Database.CreateAsync();
        var due = Now.AddMinutes(-1);
        var markId = await database.AddMark(due);
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim();
        Task Close(bool manual) => Task.Run(async () =>
        {
            await using var db = database.Open();
            var store = new CampusStore(db);
            ready.Signal();
            if (!start.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            if (manual) await store.RemoveMarkAsync(database.UserId, markId, Now);
            else await store.CloseDueMarksAsync(Now);
        });
        var first = Close(true);
        var second = Close(false);
        var bothReady = ready.Wait(TimeSpan.FromSeconds(10));
        start.Set();
        await Task.WhenAll(first, second);
        Assert.That(bothReady, Is.True);
        await using var verify = database.Open();
        var closedAt = (await verify.OccupancyMarks.AsNoTracking().SingleAsync()).ClosedAt;
        Assert.That(closedAt, Is.AnyOf(due, Now));
        var store = new CampusStore(verify);
        Assert.That(await store.CloseDueMarksAsync(Now.AddHours(1)), Is.Zero);
        Assert.That(await store.RemoveMarkAsync(database.UserId, markId, Now.AddHours(2)), Is.False);
        Assert.That((await verify.OccupancyMarks.AsNoTracking().SingleAsync()).ClosedAt, Is.EqualTo(closedAt));
    }

    [Test]
    public async Task LateAutoReleaseUsesLessonTimeAndCancellationNeedsNoCoverage()
    {
        await using var database = await Database.CreateAsync();
        var due = Now.AddMinutes(-10);
        var markId = await database.AddMark(due);
        await using var db = database.Open();
        var service = Service(db);
        Assert.That(await service.GetOwnMark(database.UserId), Is.Null);
        Assert.That((await db.OccupancyMarks.AsNoTracking().SingleAsync()).ClosedAt, Is.EqualTo(due));
        Assert.That((await service.CreateMark(database.UserId, database.FirstRoomId)).IsSuccess, Is.True);
        var own = await service.GetOwnMark(database.UserId);
        var room = await db.Rooms.SingleAsync(room => room.Id == database.FirstRoomId);
        room.ScheduleDates.Clear();
        await db.SaveChangesAsync();
        Assert.That((await service.CancelMark(database.UserId, own!.MarkId)).IsSuccess, Is.True);
    }

    [Test]
    public async Task LessonIntervalIsHalfOpenAndUncoveredRoomsCannotBeOccupied()
    {
        await using var database = await Database.CreateAsync();
        await using var db = database.Open();
        db.ScheduleEntries.AddRange(
            new Lesson(database.FirstRoomId, Now.AddHours(-1), Now),
            new Lesson(database.SecondRoomId, Now, Now.AddHours(1)));
        await db.SaveChangesAsync();
        var service = Service(db);
        Assert.That((await service.GetRoom(database.FirstRoomId))!.RoomInfo.State, Is.EqualTo(RoomState.Free));
        Assert.That((await service.GetRoom(database.SecondRoomId))!.RoomInfo.State, Is.EqualTo(RoomState.Busy));
        Assert.That((await service.CreateMark(database.UserId, database.SecondRoomId)).Code, Is.EqualTo(CampusOperationCode.RoomAlreadyOccupied));
        Assert.That((await service.CreateMark(database.UserId, database.UncoveredRoomId)).Code, Is.EqualTo(CampusOperationCode.ScheduleNotCovered));
    }

    [Test]
    public async Task RejectsClosingBeforeCreationAndReturnsMissingMark()
    {
        await using var database = await Database.CreateAsync();
        await using var db = database.Open();
        var future = new OccupancyMark(database.FirstRoomId, database.UserId, Now.AddMinutes(1));
        db.OccupancyMarks.Add(future);
        await db.SaveChangesAsync();
        var service = Service(db);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CancelMark(database.UserId, future.Id));
        Assert.That((await service.CancelMark(database.UserId, int.MaxValue)).Code,
            Is.EqualTo(CampusOperationCode.MarkNotFound));
        Assert.That((await service.CreateMark(int.MaxValue, database.SecondRoomId)).Code,
            Is.EqualTo(CampusOperationCode.UserNotFound));
        Assert.That((await service.CreateMark(database.OtherUserId, int.MaxValue)).Code,
            Is.EqualTo(CampusOperationCode.RoomNotFound));
        Assert.That((await db.OccupancyMarks.AsNoTracking().SingleAsync()).ClosedAt, Is.Null);
    }

    private static CampusService Service(AppDbContext db) => new(new CampusStore(db), new FixedClock());

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => CampusServiceTests.Now.ToUniversalTime();
        public DateTimeOffset Now => CampusServiceTests.Now;
        public DateOnly Today => CampusServiceTests.Today;
        public DateOnly GetDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(TimeSpan.FromHours(3)).DateTime);
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "auditorium-campus-" + Guid.NewGuid());
        private readonly DbContextOptions<AppDbContext> _options;
        public int UserId { get; private set; }
        public int OtherUserId { get; private set; }
        public int FirstRoomId { get; private set; }
        public int SecondRoomId { get; private set; }
        public int UncoveredRoomId { get; private set; }

        private Database()
        {
            Directory.CreateDirectory(_directory);
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={Path.Combine(_directory, "test.db")};Pooling=False;Default Timeout=3").Options;
        }

        public AppDbContext Open() => new(_options);
        public static async Task<Database> CreateAsync()
        {
            var database = new Database();
            await using var db = database.Open();
            await db.Database.MigrateAsync();
            var first = new Room("3101");
            first.ScheduleDates.Add(Today);
            var second = new Room("3102 (м)");
            second.ScheduleDates.Add(Today);
            var uncovered = new Room("3103");
            var user = new User("alpha", "fixture");
            var other = new User("bravo", "fixture");
            db.AddRange(first, second, uncovered, user, other);
            await db.SaveChangesAsync();
            database.UserId = user.Id;
            database.OtherUserId = other.Id;
            database.FirstRoomId = first.Id;
            database.SecondRoomId = second.Id;
            database.UncoveredRoomId = uncovered.Id;
            return database;
        }

        public async Task<int> AddMark(DateTimeOffset due)
        {
            await using var db = Open();
            var mark = new OccupancyMark(FirstRoomId, UserId, Now.AddHours(-1));
            mark.SetAutoReleaseAt(due);
            db.OccupancyMarks.Add(mark);
            await db.SaveChangesAsync();
            return mark.Id;
        }

        public ValueTask DisposeAsync()
        {
            Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
