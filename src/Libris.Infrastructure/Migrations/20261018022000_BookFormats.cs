using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Libris.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookFormats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_digital",
                table: "books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_physical",
                table: "books",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_digital",
                table: "books");

            migrationBuilder.DropColumn(
                name: "is_physical",
                table: "books");
        }
    }
}
