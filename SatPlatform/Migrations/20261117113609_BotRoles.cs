using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SatPlatform.Migrations
{
    /// <inheritdoc />
    public partial class BotRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BotRole",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanUseSite",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramFileId",
                table: "MentorQuestions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotRole",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanUseSite",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramFileId",
                table: "MentorQuestions");
        }
    }
}
