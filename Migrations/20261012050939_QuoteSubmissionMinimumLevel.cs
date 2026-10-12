using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations;

/// <inheritdoc />
public partial class QuoteSubmissionMinimumLevel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "QuoteSubmissionMinLevel",
            table: "Guilds",
            type: "integer",
            nullable: false,
            defaultValue: 5);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "QuoteSubmissionMinLevel",
            table: "Guilds");
    }
}
