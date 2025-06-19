using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations;

/// <inheritdoc />
public partial class buttongame : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ButtonGamePresses",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                GuildId = table.Column<int>(type: "integer", nullable: true),
                Score = table.Column<long>(type: "bigint", nullable: false),
                InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ButtonGamePresses", x => x.Id);
                table.ForeignKey(
                    name: "FK_ButtonGamePresses_Guilds_GuildId",
                    column: x => x.GuildId,
                    principalTable: "Guilds",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ButtonGamePresses_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ButtonGamePresses_GuildId",
            table: "ButtonGamePresses",
            column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_ButtonGamePresses_UserId",
            table: "ButtonGamePresses",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ButtonGamePresses");
    }
}
