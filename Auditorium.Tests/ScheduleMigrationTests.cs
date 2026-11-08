using Auditorium.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace Auditorium.Tests;

public class ScheduleMigrationTests
{
    [Test]
    public async Task MigratesEmptyDatabaseWithoutPendingModelChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);

        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
        await db.Database.MigrateAsync();
        Assert.That(await db.Database.GetPendingMigrationsAsync(), Is.Empty);
        await db.Database.MigrateAsync();
    }

    [Test]
    public async Task UpgradesExistingUtcSchemaAndPreservesOccupancyAndCoverage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.GetService<IMigrator>().MigrateAsync("20261027191206_PreserveLegacyScheduleAndUseUtcTime");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Rooms (Id, Number, Status, ScheduleDates) VALUES (5, '1201', 1, '["2026-11-08"]');
            INSERT INTO Users (Id, Login, PasswordHash) VALUES (7, 'fixture', 'fixture');
            INSERT INTO ScheduleEntries (Id, RoomId, StartsAt, EndsAt) VALUES (9, 5, 1000, 2000);
            INSERT INTO OccupancyMarks (Id, RoomId, UserId, CreatedAt) VALUES (10, 5, 7, 500);
            """);

        await db.Database.MigrateAsync();

        Assert.That((await db.Rooms.SingleAsync()).ScheduleDates, Is.EqualTo(new[] { new DateOnly(2026, 11, 8) }));
        Assert.That((await db.ScheduleEntries.SingleAsync()).RoomId, Is.EqualTo(5));
        Assert.That((await db.OccupancyMarks.SingleAsync()).UserId, Is.EqualTo(7));
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Rooms') WHERE name = 'Status'";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(0L));

        var updated = new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.FromHours(3));
        db.ScheduleImportStates.Add(new ScheduleImportState { LastSuccessfulUpdate = updated, UpdateFailed = true });
        await db.SaveChangesAsync();
        await using var reopened = CreateContext(connection);
        var state = await reopened.ScheduleImportStates.SingleAsync();
        Assert.That(state.LastSuccessfulUpdate, Is.EqualTo(updated));
        Assert.That(state.UpdateFailed, Is.True);
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
}
