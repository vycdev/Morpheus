using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class UniqueQuoteScoresPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH ranked_scores AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY "QuoteId", "UserId"
                            ORDER BY COALESCE("UpdateDate", "InsertDate") DESC, "Id" DESC
                        ) AS "Rank"
                    FROM "QuoteScores"
                )
                DELETE FROM "QuoteScores" qs
                USING ranked_scores
                WHERE qs."Id" = ranked_scores."Id"
                  AND ranked_scores."Rank" > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_QuoteScores_QuoteId",
                table: "QuoteScores");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteScores_QuoteId_UserId",
                table: "QuoteScores",
                columns: new[] { "QuoteId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuoteScores_QuoteId_UserId",
                table: "QuoteScores");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteScores_QuoteId",
                table: "QuoteScores",
                column: "QuoteId");
        }
    }
}
