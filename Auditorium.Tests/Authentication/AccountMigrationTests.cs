using NUnit.Framework;
using NUnit.Framework.Legacy;
using Auditorium.Core.Entities;
using Auditorium.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Auditorium.Tests.Authentication;

public sealed class AccountMigrationTests
{
    [Test]
    public async Task Login_migration_preserves_user_id_hash_and_mark_foreign_key()
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        await db.GetService<IMigrator>().MigrateAsync("20261108130929_MapRoomsByScheduleCode");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Rooms (Id, Number, ScheduleDates) VALUES (15, '3101', '[]');
            INSERT INTO Users (Id, Login, PasswordHash) VALUES (42, 'Alice', 'unchanged-hash');
            INSERT INTO OccupancyMarks (Id, RoomId, UserId, CreatedAt) VALUES (17, 15, 42, 500);
            """);
        await db.Database.MigrateAsync();
        var user = await db.Users.SingleAsync(u => u.Login == "ALICE");
        ClassicAssert.AreEqual(42, user.Id);
        ClassicAssert.AreEqual("Alice", user.Login);
        ClassicAssert.AreEqual("unchanged-hash", user.PasswordHash);
        ClassicAssert.AreEqual(42, (await db.OccupancyMarks.SingleAsync()).UserId);
        ClassicAssert.IsEmpty(await db.Database.SqlQueryRaw<ForeignKeyProblem>("PRAGMA foreign_key_check").ToListAsync());
    }

    [Test]
    public async Task Conflicting_legacy_logins_stop_before_any_user_or_mark_is_changed()
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        await db.GetService<IMigrator>().MigrateAsync("20261108130929_MapRoomsByScheduleCode");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Users (Id, Login, PasswordHash) VALUES (10, 'Alice', 'hash-a'), (11, 'alice', 'hash-b');
            """);
        Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.MigrateAsync());
        await using var reopened = database.Open();
        var rows = await reopened.Database.SqlQueryRaw<string>("SELECT Id || ':' || Login || ':' || PasswordHash AS Value FROM Users ORDER BY Id").ToListAsync();
        ClassicAssert.AreEqual(new[] { "10:Alice:hash-a", "11:alice:hash-b" }, rows);
        Assert.That(await reopened.Database.GetPendingMigrationsAsync(), Has.Exactly(1).Items);
    }

    [Test]
    public async Task Fresh_database_migrates_and_snapshot_matches_model()
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        ClassicAssert.IsFalse(db.Database.HasPendingModelChanges());
        await db.Database.MigrateAsync();
        db.Users.Add(new User("alice", new IdentityPasswordHasher().Hash("password1")));
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        Assert.That(await db.Users.ToListAsync(), Has.Exactly(1).Items);
        ClassicAssert.IsEmpty(await db.Database.GetPendingMigrationsAsync());
        var integrity = await db.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync();
        ClassicAssert.AreEqual("ok", integrity.Single());
    }

    [Test]
    public async Task Historical_schedule_rows_and_relations_survive_upgrade()
    {
        using var database = new TestDatabase();
        await using var db = database.Open();
        await db.GetService<IMigrator>().MigrateAsync("20260930130609_AddAudiencesTable");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Audiences (Id, Name) VALUES (7, '101');
            INSERT INTO Lessons (Id, AudienceId, Subject, GroupName, Semester)
            VALUES (11, 7, 'Алгоритмы', 'ПИН-311', '2026');
            """);
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        var rows = await db.Database.SqlQueryRaw<string>(
            "SELECT Subject || ':' || GroupName || ':' || Semester || ':' || LegacyAudiences.Name AS Value FROM LegacyLessons JOIN LegacyAudiences ON LegacyLessons.AudienceId=LegacyAudiences.Id").ToListAsync();
        ClassicAssert.AreEqual("Алгоритмы:ПИН-311:2026:101", rows.Single());
        ClassicAssert.IsEmpty(await db.Database.SqlQueryRaw<ForeignKeyProblem>("PRAGMA foreign_key_check").ToListAsync());
        // Старое расписание не выдаём за достоверное текущее календарное расписание.
        ClassicAssert.IsEmpty(await db.ScheduleEntries.ToListAsync());
    }

    private sealed class ForeignKeyProblem
    {
        public string Table { get; set; } = "";
        public long Rowid { get; set; }
        public string Parent { get; set; } = "";
        public long Fkid { get; set; }
    }
}
