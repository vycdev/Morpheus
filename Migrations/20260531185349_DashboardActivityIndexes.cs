using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class DashboardActivityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserActivity_GuildId",
                table: "UserActivity");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_DiscordChannelId_InsertDate",
                table: "UserActivity",
                columns: new[] { "DiscordChannelId", "InsertDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_GuildId_InsertDate",
                table: "UserActivity",
                columns: new[] { "GuildId", "InsertDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_InsertDate",
                table: "UserActivity",
                column: "InsertDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserActivity_DiscordChannelId_InsertDate",
                table: "UserActivity");

            migrationBuilder.DropIndex(
                name: "IX_UserActivity_GuildId_InsertDate",
                table: "UserActivity");

            migrationBuilder.DropIndex(
                name: "IX_UserActivity_InsertDate",
                table: "UserActivity");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_GuildId",
                table: "UserActivity",
                column: "GuildId");
        }
    }
}
