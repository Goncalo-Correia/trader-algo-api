using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBacktest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BacktestId",
                table: "trades",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "backtests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SymbolId = table.Column<int>(type: "integer", nullable: false),
                    IntervalId = table.Column<int>(type: "integer", nullable: false),
                    TradingStrategyId = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InitialBalance = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    FinalBalance = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Pnl = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    CandleCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backtests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_backtests_intervals_IntervalId",
                        column: x => x.IntervalId,
                        principalTable: "intervals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_backtests_symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_backtests_trading_strategies_TradingStrategyId",
                        column: x => x.TradingStrategyId,
                        principalTable: "trading_strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trades_BacktestId",
                table: "trades",
                column: "BacktestId");

            migrationBuilder.CreateIndex(
                name: "IX_backtests_IntervalId",
                table: "backtests",
                column: "IntervalId");

            migrationBuilder.CreateIndex(
                name: "IX_backtests_SymbolId",
                table: "backtests",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_backtests_TradingStrategyId",
                table: "backtests",
                column: "TradingStrategyId");

            migrationBuilder.AddForeignKey(
                name: "FK_trades_backtests_BacktestId",
                table: "trades",
                column: "BacktestId",
                principalTable: "backtests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trades_backtests_BacktestId",
                table: "trades");

            migrationBuilder.DropTable(
                name: "backtests");

            migrationBuilder.DropIndex(
                name: "IX_trades_BacktestId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "BacktestId",
                table: "trades");
        }
    }
}
