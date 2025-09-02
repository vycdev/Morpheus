using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class userlevelsrollingaveragemessagelength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "UserAverageMessageLength",
                table: "UserLevels",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "UserAverageMessageLengthEma",
                table: "UserLevels",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "UserMessageCount",
                table: "UserLevels",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserAverageMessageLength",
                table: "UserLevels");

            migrationBuilder.DropColumn(
                name: "UserAverageMessageLengthEma",
                table: "UserLevels");

            migrationBuilder.DropColumn(
                name: "UserMessageCount",
                table: "UserLevels");
        }
    }
}
