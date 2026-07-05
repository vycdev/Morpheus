using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class RssAndTwitchSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RssSeenEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedUrl = table.Column<string>(type: "text", nullable: false),
                    EntryId = table.Column<string>(type: "text", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RssSeenEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RssSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    FeedUrl = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    WebhookId = table.Column<int>(type: "integer", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RssSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RssSubscriptions_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TwitchSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TwitchUserId = table.Column<string>(type: "text", nullable: false),
                    TwitchLogin = table.Column<string>(type: "text", nullable: false),
                    TwitchDisplayName = table.Column<string>(type: "text", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false),
                    LastAnnouncedStreamId = table.Column<string>(type: "text", nullable: true),
                    WebhookId = table.Column<int>(type: "integer", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitchSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwitchSubscriptions_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RssSeenEntries_FeedUrl_EntryId",
                table: "RssSeenEntries",
                columns: new[] { "FeedUrl", "EntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RssSubscriptions_ChannelDiscordId_FeedUrl",
                table: "RssSubscriptions",
                columns: new[] { "ChannelDiscordId", "FeedUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RssSubscriptions_FeedUrl",
                table: "RssSubscriptions",
                column: "FeedUrl");

            migrationBuilder.CreateIndex(
                name: "IX_RssSubscriptions_WebhookId",
                table: "RssSubscriptions",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitchSubscriptions_ChannelDiscordId_TwitchUserId",
                table: "TwitchSubscriptions",
                columns: new[] { "ChannelDiscordId", "TwitchUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TwitchSubscriptions_TwitchUserId",
                table: "TwitchSubscriptions",
                column: "TwitchUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitchSubscriptions_WebhookId",
                table: "TwitchSubscriptions",
                column: "WebhookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RssSeenEntries");

            migrationBuilder.DropTable(
                name: "RssSubscriptions");

            migrationBuilder.DropTable(
                name: "TwitchSubscriptions");
        }
    }
}
