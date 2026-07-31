using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderDeliveryBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryFailureCount",
                table: "Reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstDeliveryFailureAt",
                table: "Reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextDeliveryAttemptAt",
                table: "Reminders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryFailureCount",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "FirstDeliveryFailureAt",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "NextDeliveryAttemptAt",
                table: "Reminders");
        }
    }
}
