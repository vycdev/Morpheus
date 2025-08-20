using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations;

/// <inheritdoc />
public partial class AddedUserActivity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UseGlobalLevelUpMessages",
            table: "Guilds");

        migrationBuilder.AddColumn<int>(
            name: "Level",
            table: "Users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "LevelUpMessages",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "LevelUpQuotes",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<long>(
            name: "TotalXp",
            table: "Users",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "User_Activity",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                GuildId = table.Column<int>(type: "integer", nullable: false),
                ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                MessageHash = table.Column<string>(type: "text", nullable: false),
                MessageLength = table.Column<int>(type: "integer", nullable: false),
                XpGained = table.Column<int>(type: "integer", nullable: false),
                InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_User_Activity", x => x.Id);
                table.ForeignKey(
                    name: "FK_User_Activity_Guilds_GuildId",
                    column: x => x.GuildId,
                    principalTable: "Guilds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_User_Activity_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_User_Activity_GuildId",
            table: "User_Activity",
            column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_User_Activity_UserId",
            table: "User_Activity",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "User_Activity");

        migrationBuilder.DropColumn(
            name: "Level",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "LevelUpMessages",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "LevelUpQuotes",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "TotalXp",
            table: "Users");

        migrationBuilder.AddColumn<bool>(
            name: "UseGlobalLevelUpMessages",
            table: "Guilds",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }
}
