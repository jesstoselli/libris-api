using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Libris.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Library : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateTable(
                name: "covers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_covers", x => x.id);
                    table.ForeignKey(
                        name: "FK_covers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genres", x => x.id);
                    table.ForeignKey(
                        name: "FK_genres_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "books",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    authors = table.Column<List<string>>(type: "text[]", nullable: false),
                    isbn = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    total_pages = table.Column<int>(type: "integer", nullable: true),
                    publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    published_year = table.Column<int>(type: "integer", nullable: true),
                    synopsis = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    cover_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ownership = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    started_at = table.Column<DateOnly>(type: "date", nullable: true),
                    finished_at = table.Column<DateOnly>(type: "date", nullable: true),
                    added_at = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_books", x => x.id);
                    table.ForeignKey(
                        name: "FK_books_covers_cover_id",
                        column: x => x.cover_id,
                        principalTable: "covers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_books_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_genres",
                columns: table => new
                {
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    genre_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_genres", x => new { x.book_id, x.genre_id });
                    table.ForeignKey(
                        name: "FK_book_genres_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_genres_genres_genre_id",
                        column: x => x.genre_id,
                        principalTable: "genres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reading_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reading_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_reading_logs_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_genres_genre_id",
                table: "book_genres",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "IX_books_cover_id",
                table: "books",
                column: "cover_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_user_isbn",
                table: "books",
                columns: new[] { "user_id", "isbn" },
                unique: true,
                filter: "isbn IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_covers_user_id",
                table: "covers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_genres_user_id",
                table: "genres",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reading_logs_book_date",
                table: "reading_logs",
                columns: new[] { "book_id", "date" });

            // Backstop for the service-level check; EF cannot model an expression index.
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_genres_user_lower_name ON genres (user_id, lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_genres_user_lower_name;");

            migrationBuilder.DropTable(
                name: "book_genres");

            migrationBuilder.DropTable(
                name: "reading_logs");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "books");

            migrationBuilder.DropTable(
                name: "covers");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}
