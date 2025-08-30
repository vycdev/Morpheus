using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class simhashforactivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuoteApprovals_QuoteApprovalMessageId",
                table: "QuoteApprovals");

            migrationBuilder.AddColumn<decimal>(
                name: "MessageSimHash",
                table: "UserActivity",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "NormalizedLength",
                table: "UserActivity",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteApprovals_QuoteApprovalMessageId_UserId",
                table: "QuoteApprovals",
                columns: new[] { "QuoteApprovalMessageId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuoteApprovals_QuoteApprovalMessageId_UserId",
                table: "QuoteApprovals");

            migrationBuilder.DropColumn(
                name: "MessageSimHash",
                table: "UserActivity");

            migrationBuilder.DropColumn(
                name: "NormalizedLength",
                table: "UserActivity");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteApprovals_QuoteApprovalMessageId",
                table: "QuoteApprovals",
                column: "QuoteApprovalMessageId");
        }
    }
}
