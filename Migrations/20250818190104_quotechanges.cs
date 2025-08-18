using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class quotechanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Approved",
                table: "Quotes");

            migrationBuilder.RenameColumn(
                name: "Removed",
                table: "Quotes",
                newName: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Active",
                table: "Quotes",
                newName: "Removed");

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                table: "Quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
