using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OstQuiz.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RawgId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Slug = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Genres = table.Column<List<string>>(type: "text[]", nullable: false),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MetacriticScore = table.Column<int>(type: "integer", nullable: true),
                    Publisher = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Developer = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CoverImageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    EnrichedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Puzzles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PuzzleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    AudioKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Puzzles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Puzzles_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PuzzleClips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PuzzleId = table.Column<int>(type: "integer", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleClips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuzzleClips_Puzzles_PuzzleId",
                        column: x => x.PuzzleId,
                        principalTable: "Puzzles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_Name",
                table: "Games",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Games_RawgId",
                table: "Games",
                column: "RawgId",
                unique: true,
                filter: "\"RawgId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleClips_PuzzleId_Step",
                table: "PuzzleClips",
                columns: new[] { "PuzzleId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_GameId",
                table: "Puzzles",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_PuzzleDate",
                table: "Puzzles",
                column: "PuzzleDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PuzzleClips");

            migrationBuilder.DropTable(
                name: "Puzzles");

            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
