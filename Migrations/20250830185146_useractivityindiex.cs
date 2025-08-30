using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class useractivityindiex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserActivity_UserId",
                table: "UserActivity");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_UserId_GuildId_InsertDate",
                table: "UserActivity",
                columns: new[] { "UserId", "GuildId", "InsertDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserActivity_UserId_GuildId_InsertDate",
                table: "UserActivity");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_UserId",
                table: "UserActivity",
                column: "UserId");
        }
    }
}
