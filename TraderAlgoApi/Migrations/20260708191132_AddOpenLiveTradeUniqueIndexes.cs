using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenLiveTradeUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_trades_open_live_account_unique",
                table: "trades",
                column: "TradingAccountId",
                unique: true,
                filter: "\"BacktestId\" IS NULL AND \"TradingAccountId\" IS NOT NULL AND (\"StatusId\" = 1 OR \"StatusId\" = 2)");

            migrationBuilder.CreateIndex(
                name: "IX_trades_open_live_symbol_unique",
                table: "trades",
                column: "SymbolId",
                unique: true,
                filter: "\"BacktestId\" IS NULL AND \"TradingAccountId\" IS NULL AND (\"StatusId\" = 1 OR \"StatusId\" = 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trades_open_live_account_unique",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "IX_trades_open_live_symbol_unique",
                table: "trades");
        }
    }
}
