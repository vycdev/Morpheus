using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class quoteapprovalsupdate5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteApprovals_Quotes_QuoteId",
                table: "QuoteApprovals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuoteApprovals",
                table: "QuoteApprovals");

            migrationBuilder.RenameTable(
                name: "QuoteApprovals",
                newName: "QuoteApproval");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteApprovals_QuoteId",
                table: "QuoteApproval",
                newName: "IX_QuoteApproval_QuoteId");

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                table: "QuoteApproval",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuoteApproval",
                table: "QuoteApproval",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteApproval_Quotes_QuoteId",
                table: "QuoteApproval",
                column: "QuoteId",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteApproval_Quotes_QuoteId",
                table: "QuoteApproval");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuoteApproval",
                table: "QuoteApproval");

            migrationBuilder.DropColumn(
                name: "Approved",
                table: "QuoteApproval");

            migrationBuilder.RenameTable(
                name: "QuoteApproval",
                newName: "QuoteApprovals");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteApproval_QuoteId",
                table: "QuoteApprovals",
                newName: "IX_QuoteApprovals_QuoteId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuoteApprovals",
                table: "QuoteApprovals",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteApprovals_Quotes_QuoteId",
                table: "QuoteApprovals",
                column: "QuoteId",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
