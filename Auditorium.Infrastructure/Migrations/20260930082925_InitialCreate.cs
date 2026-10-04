using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditorium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Day = table.Column<int>(type: "INTEGER", nullable: true),
                    DayNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Time = table.Column<string>(type: "TEXT", nullable: true),
                    TimeFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TimeTo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TeacherFull = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Teacher = table.Column<string>(type: "TEXT", nullable: true),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RoomCode = table.Column<int>(type: "INTEGER", nullable: true),
                    RoomName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AudienceId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lessons");
        }
    }
}
