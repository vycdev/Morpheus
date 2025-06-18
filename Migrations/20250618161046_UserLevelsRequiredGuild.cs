using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class UserLevelsRequiredGuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserLevels_Guilds_GuildId",
                table: "UserLevels");

            migrationBuilder.AlterColumn<int>(
                name: "GuildId",
                table: "UserLevels",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserLevels_Guilds_GuildId",
                table: "UserLevels",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserLevels_Guilds_GuildId",
                table: "UserLevels");

            migrationBuilder.AlterColumn<int>(
                name: "GuildId",
                table: "UserLevels",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_UserLevels_Guilds_GuildId",
                table: "UserLevels",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id");
        }
    }
}
