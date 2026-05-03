using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingStrategyAndAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TradingAccountId",
                table: "trades",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trading_strategies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trading_strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trading_accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartingAmount = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    TradingStrategyId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trading_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trading_accounts_trading_strategies_TradingStrategyId",
                        column: x => x.TradingStrategyId,
                        principalTable: "trading_strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "trading_strategies",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "SMA" },
                    { 2, "RSI" },
                    { 3, "MACD" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_trades_TradingAccountId",
                table: "trades",
                column: "TradingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_trading_accounts_TradingStrategyId",
                table: "trading_accounts",
                column: "TradingStrategyId");

            migrationBuilder.AddForeignKey(
                name: "FK_trades_trading_accounts_TradingAccountId",
                table: "trades",
                column: "TradingAccountId",
                principalTable: "trading_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trades_trading_accounts_TradingAccountId",
                table: "trades");

            migrationBuilder.DropTable(
                name: "trading_accounts");

            migrationBuilder.DropTable(
                name: "trading_strategies");

            migrationBuilder.DropIndex(
                name: "IX_trades_TradingAccountId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "TradingAccountId",
                table: "trades");
        }
    }
}
