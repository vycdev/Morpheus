using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class quoteapprovalsnew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuoteApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteApprovalMessageId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteApprovals_QuoteApprovalMessages_QuoteApprovalMessageId",
                        column: x => x.QuoteApprovalMessageId,
                        principalTable: "QuoteApprovalMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteApprovals_QuoteApprovalMessageId",
                table: "QuoteApprovals",
                column: "QuoteApprovalMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteApprovals");
        }
    }
}
