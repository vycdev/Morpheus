using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class reactroles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReactionRoleMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionRoleMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactionRoleMessages_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReactionRoleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReactionRoleMessageId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Emoji = table.Column<string>(type: "text", nullable: false),
                    CustomId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionRoleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactionRoleItems_ReactionRoleMessages_ReactionRoleMessageId",
                        column: x => x.ReactionRoleMessageId,
                        principalTable: "ReactionRoleMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReactionRoleItems_ReactionRoleMessageId_Emoji",
                table: "ReactionRoleItems",
                columns: new[] { "ReactionRoleMessageId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReactionRoleItems_ReactionRoleMessageId_RoleId",
                table: "ReactionRoleItems",
                columns: new[] { "ReactionRoleMessageId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReactionRoleMessages_GuildId",
                table: "ReactionRoleMessages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ReactionRoleMessages_MessageId",
                table: "ReactionRoleMessages",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReactionRoleItems");

            migrationBuilder.DropTable(
                name: "ReactionRoleMessages");
        }
    }
}
