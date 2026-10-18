using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Libris.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedGenres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // From here on a genre leaves with its last book; this clears the ones already left behind.
            migrationBuilder.Sql("DELETE FROM genres g WHERE NOT EXISTS (SELECT 1 FROM book_genres bg WHERE bg.genre_id = g.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The removed genres had no books, so there is nothing to put back.
        }
    }
}
