using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddNySessionAndDailyLimitsToBacktest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNySessionOnly",
                table: "backtests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyProfitGoal",
                table: "backtests",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxLossesPerDay",
                table: "backtests",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNySessionOnly",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "DailyProfitGoal",
                table: "backtests");

            migrationBuilder.DropColumn(
                name: "MaxLossesPerDay",
                table: "backtests");
        }
    }
}
