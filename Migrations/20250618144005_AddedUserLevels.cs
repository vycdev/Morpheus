using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations;

/// <inheritdoc />
public partial class AddedUserLevels : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Level",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "TotalXp",
            table: "Users");

        migrationBuilder.CreateTable(
            name: "UserLevels",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                GuildId = table.Column<int>(type: "integer", nullable: true),
                Level = table.Column<int>(type: "integer", nullable: false),
                Xp = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserLevels", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserLevels_Guilds_GuildId",
                    column: x => x.GuildId,
                    principalTable: "Guilds",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserLevels_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserLevels_GuildId",
            table: "UserLevels",
            column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_UserLevels_UserId",
            table: "UserLevels",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserLevels");

        migrationBuilder.AddColumn<int>(
            name: "Level",
            table: "Users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "TotalXp",
            table: "Users",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);
    }
}
