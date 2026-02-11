using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class AddStocksFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Users",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscordId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DailyChangePercent = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UpdateTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockHoldings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StockId = table.Column<int>(type: "integer", nullable: false),
                    Shares = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TotalInvested = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockHoldings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockHoldings_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockHoldings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StockId = table.Column<int>(type: "integer", nullable: true),
                    TargetUserId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Shares = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    PriceAtTransaction = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    InsertDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransactions_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockTransactions_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_DiscordId",
                table: "Channels",
                column: "DiscordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockHoldings_StockId",
                table: "StockHoldings",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_StockHoldings_UserId_StockId",
                table: "StockHoldings",
                columns: new[] { "UserId", "StockId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_EntityType_EntityId",
                table: "Stocks",
                columns: new[] { "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_LastUpdatedDate_UpdateTimeMinutes",
                table: "Stocks",
                columns: new[] { "LastUpdatedDate", "UpdateTimeMinutes" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_StockId",
                table: "StockTransactions",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_TargetUserId",
                table: "StockTransactions",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_UserId_InsertDate",
                table: "StockTransactions",
                columns: new[] { "UserId", "InsertDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "StockHoldings");

            migrationBuilder.DropTable(
                name: "StockTransactions");

            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Users");
        }
    }
}
