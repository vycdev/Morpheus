using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations;

/// <inheritdoc />
public partial class guildremoveforeignkey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Users_Guilds_GuildId",
            table: "Users");

        migrationBuilder.DropIndex(
            name: "IX_Users_GuildId",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "GuildId",
            table: "Users");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "GuildId",
            table: "Users",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_GuildId",
            table: "Users",
            column: "GuildId");

        migrationBuilder.AddForeignKey(
            name: "FK_Users_Guilds_GuildId",
            table: "Users",
            column: "GuildId",
            principalTable: "Guilds",
            principalColumn: "Id");
    }
}
