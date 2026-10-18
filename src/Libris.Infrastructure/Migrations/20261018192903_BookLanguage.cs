using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Libris.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "books",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "pt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "language",
                table: "books");
        }
    }
}
