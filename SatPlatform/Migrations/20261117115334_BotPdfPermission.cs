using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatPlatform.Migrations
{
    /// <inheritdoc />
    public partial class BotPdfPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanManageBotPdfs",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanManageBotPdfs",
                table: "Users");
        }
    }
}
