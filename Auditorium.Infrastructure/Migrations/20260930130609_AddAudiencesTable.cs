using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditorium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudiencesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audiences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audiences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_AudienceId",
                table: "Lessons",
                column: "AudienceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Audiences_AudienceId",
                table: "Lessons",
                column: "AudienceId",
                principalTable: "Audiences",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Audiences_AudienceId",
                table: "Lessons");

            migrationBuilder.DropTable(
                name: "Audiences");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_AudienceId",
                table: "Lessons");
        }
    }
}
