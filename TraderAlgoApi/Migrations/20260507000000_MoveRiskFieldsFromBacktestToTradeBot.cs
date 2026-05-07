using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class MoveRiskFieldsFromBacktestToTradeBot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Breakeven",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "DailyProfitGoal",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "IsNySessionOnly",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "MaxCandlesPerTrade",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "MaxLossesPerDay",
                table: "backtests");

            migrationBuilder.AddColumn<decimal>(
                name: "Breakeven",
                table: "trade_bots",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyProfitGoal",
                table: "trade_bots",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNySessionOnly",
                table: "trade_bots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxCandlesPerTrade",
                table: "trade_bots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxLossesPerDay",
                table: "trade_bots",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Breakeven",
                table: "trade_bots");

            migrationBuilder.DropColumn(
                name: "DailyProfitGoal",
                table: "trade_bots");

            migrationBuilder.DropColumn(
                name: "IsNySessionOnly",
                table: "trade_bots");

            migrationBuilder.DropColumn(
                name: "MaxCandlesPerTrade",
                table: "trade_bots");

            migrationBuilder.DropColumn(
                name: "MaxLossesPerDay",
                table: "trade_bots");

            migrationBuilder.AddColumn<decimal>(
                name: "Breakeven",
                table: "backtests",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyProfitGoal",
                table: "backtests",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNySessionOnly",
                table: "backtests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxCandlesPerTrade",
                table: "backtests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxLossesPerDay",
                table: "backtests",
                type: "integer",
                nullable: true);
        }
    }
}
