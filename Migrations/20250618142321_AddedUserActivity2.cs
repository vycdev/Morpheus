using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace Morpheus.Migrations;

/// <inheritdoc />
public partial class AddedUserActivity2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "User_Activity");

        migrationBuilder.CreateTable(
            name: "UserActivity",
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
                table.PrimaryKey("PK_UserActivity", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserActivity_Guilds_GuildId",
                    column: x => x.GuildId,
                    principalTable: "Guilds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserActivity_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserActivity_GuildId",
            table: "UserActivity",
            column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_UserActivity_UserId",
            table: "UserActivity",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserActivity");

        migrationBuilder.CreateTable(
            name: "User_Activity",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<int>(type: "integer", nullable: false),
                ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                MessageHash = table.Column<string>(type: "text", nullable: false),
                MessageLength = table.Column<int>(type: "integer", nullable: false),
                XpGained = table.Column<int>(type: "integer", nullable: false)
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
}
