using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class robbery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "Users",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 1000.00m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRobberyAttempt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfullyRobbed",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRobberyAttempt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastSuccessfullyRobbed",
                table: "Users");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "Users",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldDefaultValue: 1000.00m);
        }
    }
}
