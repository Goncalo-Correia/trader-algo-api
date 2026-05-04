using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTradeBotScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trade_bots_TradingAccountId",
                table: "trade_bots");

            migrationBuilder.AlterColumn<long>(
                name: "TradingAccountId",
                table: "trade_bots",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "BacktestId",
                table: "trade_bots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TradingStrategyId",
                table: "trade_bots",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE trade_bots AS b
                SET "TradingStrategyId" = a."TradingStrategyId"
                FROM trading_accounts AS a
                WHERE b."TradingAccountId" = a."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_trade_bots_BacktestId",
                table: "trade_bots",
                column: "BacktestId",
                unique: true,
                filter: "\"BacktestId\" IS NOT NULL AND \"IsEnabled\"");

            migrationBuilder.CreateIndex(
                name: "IX_trade_bots_TradingAccountId",
                table: "trade_bots",
                column: "TradingAccountId",
                unique: true,
                filter: "\"TradingAccountId\" IS NOT NULL AND \"IsEnabled\"");

            migrationBuilder.CreateIndex(
                name: "IX_trade_bots_TradingStrategyId",
                table: "trade_bots",
                column: "TradingStrategyId");

            migrationBuilder.AddForeignKey(
                name: "FK_trade_bots_backtests_BacktestId",
                table: "trade_bots",
                column: "BacktestId",
                principalTable: "backtests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trade_bots_trading_strategies_TradingStrategyId",
                table: "trade_bots",
                column: "TradingStrategyId",
                principalTable: "trading_strategies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trade_bots_backtests_BacktestId",
                table: "trade_bots");

            migrationBuilder.DropForeignKey(
                name: "FK_trade_bots_trading_strategies_TradingStrategyId",
                table: "trade_bots");

            migrationBuilder.DropIndex(
                name: "IX_trade_bots_BacktestId",
                table: "trade_bots");

            migrationBuilder.DropIndex(
                name: "IX_trade_bots_TradingAccountId",
                table: "trade_bots");

            migrationBuilder.DropIndex(
                name: "IX_trade_bots_TradingStrategyId",
                table: "trade_bots");

            migrationBuilder.DropColumn(
                name: "BacktestId",
                table: "trade_bots");

            migrationBuilder.DropColumn(
                name: "TradingStrategyId",
                table: "trade_bots");

            migrationBuilder.AlterColumn<long>(
                name: "TradingAccountId",
                table: "trade_bots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_trade_bots_TradingAccountId",
                table: "trade_bots",
                column: "TradingAccountId",
                unique: true);
        }
    }
}
