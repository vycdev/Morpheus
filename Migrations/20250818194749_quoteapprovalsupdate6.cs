using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class quoteapprovalsupdate6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteApproval_Quotes_QuoteId",
                table: "QuoteApproval");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteScore_Quotes_QuoteId",
                table: "QuoteScore");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteScore_Users_UserId",
                table: "QuoteScore");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuoteScore",
                table: "QuoteScore");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuoteApproval",
                table: "QuoteApproval");

            migrationBuilder.RenameTable(
                name: "QuoteScore",
                newName: "QuoteScores");

            migrationBuilder.RenameTable(
                name: "QuoteApproval",
                newName: "QuoteApprovals");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteScore_UserId",
                table: "QuoteScores",
                newName: "IX_QuoteScores_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteScore_QuoteId",
                table: "QuoteScores",
                newName: "IX_QuoteScores_QuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteApproval_QuoteId",
                table: "QuoteApprovals",
                newName: "IX_QuoteApprovals_QuoteId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuoteScores",
                table: "QuoteScores",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteScores_Quotes_QuoteId",
                table: "QuoteScores",
                column: "QuoteId",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteScores_Users_UserId",
                table: "QuoteScores",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteApprovals_Quotes_QuoteId",
                table: "QuoteApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteScores_Quotes_QuoteId",
                table: "QuoteScores");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteScores_Users_UserId",
                table: "QuoteScores");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuoteScores",
                table: "QuoteScores");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuoteApprovals",
                table: "QuoteApprovals");

            migrationBuilder.RenameTable(
                name: "QuoteScores",
                newName: "QuoteScore");

            migrationBuilder.RenameTable(
                name: "QuoteApprovals",
                newName: "QuoteApproval");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteScores_UserId",
                table: "QuoteScore",
                newName: "IX_QuoteScore_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteScores_QuoteId",
                table: "QuoteScore",
                newName: "IX_QuoteScore_QuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_QuoteApprovals_QuoteId",
                table: "QuoteApproval",
                newName: "IX_QuoteApproval_QuoteId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuoteScore",
                table: "QuoteScore",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteScore_Quotes_QuoteId",
                table: "QuoteScore",
                column: "QuoteId",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteScore_Users_UserId",
                table: "QuoteScore",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
