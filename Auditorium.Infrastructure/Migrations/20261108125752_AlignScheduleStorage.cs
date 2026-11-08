using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditorium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignScheduleStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite поддерживает DROP COLUMN. Таблица и её внешние ключи остаются на месте.
            migrationBuilder.Sql("""ALTER TABLE "Rooms" DROP COLUMN "Status";""");

            migrationBuilder.CreateTable(
                name: "ScheduleImportState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSuccessfulUpdate = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdateFailed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleImportState", x => x.Id);
                    table.CheckConstraint("CK_ScheduleImportState_Singleton", "Id = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleImportState");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Rooms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
