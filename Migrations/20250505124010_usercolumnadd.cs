﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations;

/// <inheritdoc />
public partial class usercolumnadd : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastUsernameCheck",
            table: "Users",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastUsernameCheck",
            table: "Users");
    }
}
