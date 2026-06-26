using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OstQuiz.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlbumCoverKey",
                table: "Puzzles",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlbumCoverKey",
                table: "Puzzles");
        }
    }
}
