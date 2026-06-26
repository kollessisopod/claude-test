using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OstQuiz.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFranchise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Franchise",
                table: "Games",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_Franchise",
                table: "Games",
                column: "Franchise");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_Franchise",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Franchise",
                table: "Games");
        }
    }
}
