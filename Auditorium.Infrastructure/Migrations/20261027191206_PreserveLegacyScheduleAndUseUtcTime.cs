using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditorium.Infrastructure.Migrations;

/// <summary>
/// Сохраняет исходное недельное расписание без выдуманных календарных дат.
/// Старые таблицы остаются архивом и не входят в рабочую EF-модель.
/// </summary>
public partial class PreserveLegacyScheduleAndUseUtcTime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Lessons" RENAME TO "LegacyLessons";
            ALTER TABLE "Audiences" RENAME TO "LegacyAudiences";

            CREATE TABLE "Rooms" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Rooms" PRIMARY KEY AUTOINCREMENT,
                "Number" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "ScheduleDates" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_Rooms_Number" ON "Rooms" ("Number");

            INSERT INTO "Rooms" ("Id", "Number", "Status", "ScheduleDates")
                SELECT "Id", "Name", 0, '[]' FROM "LegacyAudiences";

            CREATE TABLE "Users" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
                "Login" TEXT NOT NULL,
                "PasswordHash" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_Users_Login" ON "Users" ("Login");

            CREATE TABLE "ScheduleEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ScheduleEntries" PRIMARY KEY AUTOINCREMENT,
                "RoomId" INTEGER NOT NULL,
                "StartsAt" INTEGER NOT NULL,
                "EndsAt" INTEGER NOT NULL,
                CONSTRAINT "FK_ScheduleEntries_Rooms_RoomId" FOREIGN KEY ("RoomId")
                    REFERENCES "Rooms" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_ScheduleEntries_RoomId" ON "ScheduleEntries" ("RoomId");

            CREATE TABLE "OccupancyMarks" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OccupancyMarks" PRIMARY KEY AUTOINCREMENT,
                "RoomId" INTEGER NOT NULL,
                "UserId" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "ClosedAt" INTEGER NULL,
                "AutoReleaseAt" INTEGER NULL,
                CONSTRAINT "FK_OccupancyMarks_Rooms_RoomId" FOREIGN KEY ("RoomId")
                    REFERENCES "Rooms" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OccupancyMarks_Users_UserId" FOREIGN KEY ("UserId")
                    REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_OccupancyMarks_RoomId" ON "OccupancyMarks" ("RoomId")
                WHERE "ClosedAt" IS NULL;
            CREATE UNIQUE INDEX "IX_OccupancyMarks_UserId" ON "OccupancyMarks" ("UserId")
                WHERE "ClosedAt" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // После перехода могут появиться новые пользователи и история отметок.
        // Автоматический откат с удалением этих данных недопустим.
        throw new NotSupportedException(
            "Обратная миграция удалит новые данные. Используйте резервную копию и отдельный план переноса.");
    }
}
