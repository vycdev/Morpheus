using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class FeedSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: EF also wanted to drop the redundant single-column FK indexes
            // (IX_StockTransactions_StockId, IX_ButtonGamePresses_GuildId, IX_ButtonGamePresses_UserId),
            // which are already covered by composite indexes added in DashboardGlobalOverviewIndexes.
            // That drop was intentionally removed so this migration is purely additive and cannot
            // fail on a DB where those indexes are absent. The leftover indexes are harmless.
            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webhooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XkcdSeen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Link = table.Column<string>(type: "text", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XkcdSeen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeSeenVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YoutubeChannelId = table.Column<string>(type: "text", nullable: false),
                    VideoId = table.Column<string>(type: "text", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeSeenVideos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XkcdSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookId = table.Column<int>(type: "integer", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XkcdSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XkcdSubscriptions_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelDiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    YoutubeChannelId = table.Column<string>(type: "text", nullable: false),
                    YoutubeChannelTitle = table.Column<string>(type: "text", nullable: false),
                    YoutubeAvatarUrl = table.Column<string>(type: "text", nullable: true),
                    WebhookId = table.Column<int>(type: "integer", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YoutubeSubscriptions_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_ChannelDiscordId",
                table: "Webhooks",
                column: "ChannelDiscordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XkcdSeen_Link",
                table: "XkcdSeen",
                column: "Link",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XkcdSubscriptions_ChannelDiscordId",
                table: "XkcdSubscriptions",
                column: "ChannelDiscordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XkcdSubscriptions_WebhookId",
                table: "XkcdSubscriptions",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeSeenVideos_VideoId",
                table: "YoutubeSeenVideos",
                column: "VideoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeSubscriptions_ChannelDiscordId_YoutubeChannelId",
                table: "YoutubeSubscriptions",
                columns: new[] { "ChannelDiscordId", "YoutubeChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeSubscriptions_WebhookId",
                table: "YoutubeSubscriptions",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeSubscriptions_YoutubeChannelId",
                table: "YoutubeSubscriptions",
                column: "YoutubeChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XkcdSeen");

            migrationBuilder.DropTable(
                name: "XkcdSubscriptions");

            migrationBuilder.DropTable(
                name: "YoutubeSeenVideos");

            migrationBuilder.DropTable(
                name: "YoutubeSubscriptions");

            migrationBuilder.DropTable(
                name: "Webhooks");
        }
    }
}
