using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUserLevelsPerGuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH merged AS (
                    SELECT
                        MIN("Id") AS "KeepId",
                        "UserId",
                        "GuildId",
                        SUM("TotalXp")::integer AS "TotalXp",
                        SUM("UserMessageCount")::integer AS "UserMessageCount",
                        CASE
                            WHEN SUM("UserMessageCount") > 0
                                THEN SUM("UserAverageMessageLength" * "UserMessageCount") / SUM("UserMessageCount")
                            ELSE MAX("UserAverageMessageLength")
                        END AS "UserAverageMessageLength",
                        CASE
                            WHEN SUM("UserMessageCount") > 0
                                THEN SUM("UserAverageMessageLengthEma" * "UserMessageCount") / SUM("UserMessageCount")
                            ELSE MAX("UserAverageMessageLengthEma")
                        END AS "UserAverageMessageLengthEma"
                    FROM "UserLevels"
                    GROUP BY "UserId", "GuildId"
                    HAVING COUNT(*) > 1
                ),
                updated AS (
                    UPDATE "UserLevels" ul
                    SET
                        "TotalXp" = merged."TotalXp",
                        "Level" = FLOOR(POWER(LOG(((merged."TotalXp" + 111) / 111)::double precision), 5.0243))::integer,
                        "UserMessageCount" = merged."UserMessageCount",
                        "UserAverageMessageLength" = merged."UserAverageMessageLength",
                        "UserAverageMessageLengthEma" = merged."UserAverageMessageLengthEma"
                    FROM merged
                    WHERE ul."Id" = merged."KeepId"
                    RETURNING ul."Id"
                )
                DELETE FROM "UserLevels" ul
                USING merged
                WHERE ul."UserId" = merged."UserId"
                  AND ul."GuildId" = merged."GuildId"
                  AND ul."Id" <> merged."KeepId";
                """);

            migrationBuilder.DropIndex(
                name: "IX_UserLevels_UserId",
                table: "UserLevels");

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_UserId_GuildId",
                table: "UserLevels",
                columns: new[] { "UserId", "GuildId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLevels_UserId_GuildId",
                table: "UserLevels");

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_UserId",
                table: "UserLevels",
                column: "UserId");
        }
    }
}
