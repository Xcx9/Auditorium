using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditorium.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Проверяем конфликт ДО перестроения Users. Нельзя выбирать владельца
            // между Alice и alice автоматически: у обоих могут быть свои отметки.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__AuthLoginCheck" (
                    "Ok" INTEGER NOT NULL CONSTRAINT "Duplicate_logins_differ_only_by_case" CHECK ("Ok" = 1)
                );
                INSERT INTO "__AuthLoginCheck" ("Ok")
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM "Users" GROUP BY "Login" COLLATE NOCASE HAVING COUNT(*) > 1
                ) THEN 0 ELSE 1 END;
                DROP TABLE "__AuthLoginCheck";
                """);
            migrationBuilder.AlterColumn<string>(
                name: "Login",
                table: "Users",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Login",
                table: "Users",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldCollation: "NOCASE");
        }
    }
}
