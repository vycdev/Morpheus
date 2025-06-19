using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations;

/// <inheritdoc />
public partial class Basics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "GuildId",
            table: "Users",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Guilds",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Prefix = table.Column<string>(type: "text", nullable: false),
                WelcomeChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                PinsChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                LevelUpMessagesChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                LevelUpQuotesChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                LevelUpMessages = table.Column<bool>(type: "boolean", nullable: false),
                LevelUpQuotes = table.Column<bool>(type: "boolean", nullable: false),
                WelcomeMessages = table.Column<bool>(type: "boolean", nullable: false),
                UseGlobalQuotes = table.Column<bool>(type: "boolean", nullable: false),
                UseGlobalLevelUpMessages = table.Column<bool>(type: "boolean", nullable: false),
                QuotesApprovalChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                QuoteAddRequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                QuoteRemoveRequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                UseActivityRoles = table.Column<bool>(type: "boolean", nullable: false),
                InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Guilds", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Quotes",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<int>(type: "integer", nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                Approved = table.Column<bool>(type: "boolean", nullable: false),
                Removed = table.Column<bool>(type: "boolean", nullable: false),
                InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Quotes", x => x.Id);
                table.ForeignKey(
                    name: "FK_Quotes_Guilds_GuildId",
                    column: x => x.GuildId,
                    principalTable: "Guilds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Quotes_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "QuoteScore",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                QuoteId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<int>(type: "integer", nullable: false),
                Score = table.Column<int>(type: "integer", nullable: false),
                InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuoteScore", x => x.Id);
                table.ForeignKey(
                    name: "FK_QuoteScore_Quotes_QuoteId",
                    column: x => x.QuoteId,
                    principalTable: "Quotes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_QuoteScore_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_GuildId",
            table: "Users",
            column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_Guilds_DiscordId",
            table: "Guilds",
            column: "DiscordId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Quotes_GuildId",
            table: "Quotes",
            column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_Quotes_UserId",
            table: "Quotes",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_QuoteScore_QuoteId",
            table: "QuoteScore",
            column: "QuoteId");

        migrationBuilder.CreateIndex(
            name: "IX_QuoteScore_UserId",
            table: "QuoteScore",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_Users_Guilds_GuildId",
            table: "Users",
            column: "GuildId",
            principalTable: "Guilds",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Users_Guilds_GuildId",
            table: "Users");

        migrationBuilder.DropTable(
            name: "QuoteScore");

        migrationBuilder.DropTable(
            name: "Quotes");

        migrationBuilder.DropTable(
            name: "Guilds");

        migrationBuilder.DropIndex(
            name: "IX_Users_GuildId",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "GuildId",
            table: "Users");
    }
}
