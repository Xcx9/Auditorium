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
