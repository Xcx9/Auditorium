using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditorium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MapRoomsByScheduleCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rooms_Number",
                table: "Rooms");

            migrationBuilder.AddColumn<int>(
                name: "SourceScheduleCode",
                table: "Rooms",
                type: "INTEGER",
                nullable: true);

            // The legacy import preserved MIET room codes as Rooms.Id.
            migrationBuilder.Sql("""
                UPDATE "Rooms"
                SET "SourceScheduleCode" = "Id"
                WHERE "Id" IN (SELECT "Id" FROM "LegacyAudiences");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Number",
                table: "Rooms",
                column: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_SourceScheduleCode",
                table: "Rooms",
                column: "SourceScheduleCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rooms_Number",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_SourceScheduleCode",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "SourceScheduleCode",
                table: "Rooms");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Number",
                table: "Rooms",
                column: "Number",
                unique: true);
        }
    }
}
