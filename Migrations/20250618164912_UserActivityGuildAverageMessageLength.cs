using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class UserActivityGuildAverageMessageLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "UserActivity",
                newName: "DiscordChannelId");

            migrationBuilder.AddColumn<double>(
                name: "GuildAverageMessageLength",
                table: "UserActivity",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "GuildMessageCount",
                table: "UserActivity",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuildAverageMessageLength",
                table: "UserActivity");

            migrationBuilder.DropColumn(
                name: "GuildMessageCount",
                table: "UserActivity");

            migrationBuilder.RenameColumn(
                name: "DiscordChannelId",
                table: "UserActivity",
                newName: "ChannelId");
        }
    }
}
