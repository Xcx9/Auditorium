using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditorium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSemester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Semester",
                table: "Lessons",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Semester",
                table: "Lessons");
        }
    }
}
