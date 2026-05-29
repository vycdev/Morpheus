using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class UniqueBotSettingKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH duplicate_money AS (
                    SELECT
                        MIN("Id") AS "KeepId",
                        "Key",
                        SUM(CASE
                            WHEN trim("Value") ~ '^[+-]?([0-9]+([.,][0-9]+)?|[.,][0-9]+)$'
                                THEN replace(trim("Value"), ',', '.')::numeric
                            WHEN "Key" = 'slots_vault'
                                THEN 10000.00
                            ELSE 0.00
                        END) AS "Amount",
                        MAX("UpdateDate") AS "UpdateDate"
                    FROM "BotSettings"
                    WHERE "Key" IN ('ubi_pool', 'slots_vault')
                    GROUP BY "Key"
                    HAVING COUNT(*) > 1
                )
                UPDATE "BotSettings" bs
                SET
                    "Value" = round(duplicate_money."Amount", 2)::text,
                    "UpdateDate" = duplicate_money."UpdateDate"
                FROM duplicate_money
                WHERE bs."Id" = duplicate_money."KeepId";
                """);

            migrationBuilder.Sql("""
                WITH duplicate_keys AS (
                    SELECT
                        MIN("Id") AS "KeepId",
                        "Key"
                    FROM "BotSettings"
                    WHERE "Key" NOT IN ('ubi_pool', 'slots_vault')
                    GROUP BY "Key"
                    HAVING COUNT(*) > 1
                ),
                latest_values AS (
                    SELECT DISTINCT ON (bs."Key")
                        bs."Key",
                        bs."Value",
                        bs."UpdateDate"
                    FROM "BotSettings" bs
                    INNER JOIN duplicate_keys dk ON dk."Key" = bs."Key"
                    ORDER BY bs."Key", bs."UpdateDate" DESC, bs."Id" DESC
                )
                UPDATE "BotSettings" bs
                SET
                    "Value" = latest_values."Value",
                    "UpdateDate" = latest_values."UpdateDate"
                FROM duplicate_keys
                INNER JOIN latest_values ON latest_values."Key" = duplicate_keys."Key"
                WHERE bs."Id" = duplicate_keys."KeepId";
                """);

            migrationBuilder.Sql("""
                WITH duplicates AS (
                    SELECT
                        MIN("Id") AS "KeepId",
                        "Key"
                    FROM "BotSettings"
                    GROUP BY "Key"
                    HAVING COUNT(*) > 1
                )
                DELETE FROM "BotSettings" bs
                USING duplicates
                WHERE bs."Key" = duplicates."Key"
                  AND bs."Id" <> duplicates."KeepId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BotSettings_Key",
                table: "BotSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BotSettings_Key",
                table: "BotSettings");
        }
    }
}
