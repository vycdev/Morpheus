using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Morpheus.Database;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DB))]
    [Migration("20260603210400_DashboardGlobalOverviewIndexes")]
    public partial class DashboardGlobalOverviewIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ButtonGamePresses_GuildId_InsertDate",
                table: "ButtonGamePresses",
                columns: new[] { "GuildId", "InsertDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ButtonGamePresses_InsertDate",
                table: "ButtonGamePresses",
                column: "InsertDate");

            migrationBuilder.CreateIndex(
                name: "IX_ButtonGamePresses_UserId_InsertDate",
                table: "ButtonGamePresses",
                columns: new[] { "UserId", "InsertDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_InsertDate_Severity",
                table: "Logs",
                columns: new[] { "InsertDate", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteApprovalMessages_Approved_InsertDate",
                table: "QuoteApprovalMessages",
                columns: new[] { "Approved", "InsertDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_InsertDate_Approved_Removed",
                table: "Quotes",
                columns: new[] { "InsertDate", "Approved", "Removed" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_InsertDate",
                table: "StockTransactions",
                column: "InsertDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_StockId_InsertDate",
                table: "StockTransactions",
                columns: new[] { "StockId", "InsertDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_InsertDate_DiscordChannelId",
                table: "UserActivity",
                columns: new[] { "InsertDate", "DiscordChannelId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_InsertDate_GuildId",
                table: "UserActivity",
                columns: new[] { "InsertDate", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivity_InsertDate_UserId",
                table: "UserActivity",
                columns: new[] { "InsertDate", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ButtonGamePresses_GuildId_InsertDate",
                table: "ButtonGamePresses");

            migrationBuilder.DropIndex(
                name: "IX_ButtonGamePresses_InsertDate",
                table: "ButtonGamePresses");

            migrationBuilder.DropIndex(
                name: "IX_ButtonGamePresses_UserId_InsertDate",
                table: "ButtonGamePresses");

            migrationBuilder.DropIndex(
                name: "IX_Logs_InsertDate_Severity",
                table: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_QuoteApprovalMessages_Approved_InsertDate",
                table: "QuoteApprovalMessages");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_InsertDate_Approved_Removed",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_InsertDate",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_StockId_InsertDate",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_UserActivity_InsertDate_DiscordChannelId",
                table: "UserActivity");

            migrationBuilder.DropIndex(
                name: "IX_UserActivity_InsertDate_GuildId",
                table: "UserActivity");

            migrationBuilder.DropIndex(
                name: "IX_UserActivity_InsertDate_UserId",
                table: "UserActivity");
        }
    }
}
