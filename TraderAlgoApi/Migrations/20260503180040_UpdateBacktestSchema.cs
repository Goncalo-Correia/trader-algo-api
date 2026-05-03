using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBacktestSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "backtests",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StopLoss",
                table: "backtests",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TakeProfit",
                table: "backtests",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TradeBotId",
                table: "backtests",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_backtests_TradeBotId",
                table: "backtests",
                column: "TradeBotId");

            migrationBuilder.AddForeignKey(
                name: "FK_backtests_trade_bots_TradeBotId",
                table: "backtests",
                column: "TradeBotId",
                principalTable: "trade_bots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_backtests_trade_bots_TradeBotId",
                table: "backtests");

            migrationBuilder.DropIndex(
                name: "IX_backtests_TradeBotId",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "StopLoss",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "TakeProfit",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "TradeBotId",
                table: "backtests");
        }
    }
}
