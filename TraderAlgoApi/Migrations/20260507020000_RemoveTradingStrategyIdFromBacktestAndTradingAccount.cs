using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTradingStrategyIdFromBacktestAndTradingAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_backtests_trading_strategies_TradingStrategyId",
                table: "backtests");

            migrationBuilder.DropForeignKey(
                name: "FK_trading_accounts_trading_strategies_TradingStrategyId",
                table: "trading_accounts");

            migrationBuilder.DropIndex(
                name: "IX_trading_accounts_TradingStrategyId",
                table: "trading_accounts");

            migrationBuilder.DropIndex(
                name: "IX_backtests_TradingStrategyId",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "TradingStrategyId",
                table: "trading_accounts");

            migrationBuilder.DropColumn(
                name: "TradingStrategyId",
                table: "backtests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TradingStrategyId",
                table: "trading_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TradingStrategyId",
                table: "backtests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_trading_accounts_TradingStrategyId",
                table: "trading_accounts",
                column: "TradingStrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_backtests_TradingStrategyId",
                table: "backtests",
                column: "TradingStrategyId");

            migrationBuilder.AddForeignKey(
                name: "FK_backtests_trading_strategies_TradingStrategyId",
                table: "backtests",
                column: "TradingStrategyId",
                principalTable: "trading_strategies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trading_accounts_trading_strategies_TradingStrategyId",
                table: "trading_accounts",
                column: "TradingStrategyId",
                principalTable: "trading_strategies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
