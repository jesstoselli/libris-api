using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Libris.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookSeriesAndRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "rating",
                table: "books",
                type: "numeric(2,1)",
                precision: 2,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "series",
                table: "books",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "series_number",
                table: "books",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_books_user_id_series",
                table: "books",
                columns: new[] { "user_id", "series" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_books_user_id_series",
                table: "books");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "books");

            migrationBuilder.DropColumn(
                name: "series",
                table: "books");

            migrationBuilder.DropColumn(
                name: "series_number",
                table: "books");
        }
    }
}
