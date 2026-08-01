using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class RecalculateActivityLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserLevels"
                SET "Level" = FLOOR(POWER(LOG(("TotalXp"::double precision + 111.0) / 111.0), 5.0243))::integer
                WHERE "TotalXp" >= 0
                  AND "Level" IS DISTINCT FROM FLOOR(POWER(LOG(("TotalXp"::double precision + 111.0) / 111.0), 5.0243))::integer;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserLevels"
                SET "Level" = FLOOR(POWER(LOG(FLOOR(("TotalXp"::double precision + 111.0) / 111.0)), 5.0243))::integer
                WHERE "TotalXp" >= 0
                  AND "Level" IS DISTINCT FROM FLOOR(POWER(LOG(FLOOR(("TotalXp"::double precision + 111.0) / 111.0)), 5.0243))::integer;
                """);
        }
    }
}
