using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeBots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trade_bots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingAccountId = table.Column<long>(type: "bigint", nullable: false),
                    SymbolId = table.Column<int>(type: "integer", nullable: false),
                    IntervalId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    StopLoss = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TakeProfit = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSignalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_bots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trade_bots_intervals_IntervalId",
                        column: x => x.IntervalId,
                        principalTable: "intervals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trade_bots_symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trade_bots_trading_accounts_TradingAccountId",
                        column: x => x.TradingAccountId,
                        principalTable: "trading_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trade_bots_IntervalId",
                table: "trade_bots",
                column: "IntervalId");

            migrationBuilder.CreateIndex(
                name: "IX_trade_bots_SymbolId",
                table: "trade_bots",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_trade_bots_TradingAccountId",
                table: "trade_bots",
                column: "TradingAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trade_bots");
        }
    }
}
