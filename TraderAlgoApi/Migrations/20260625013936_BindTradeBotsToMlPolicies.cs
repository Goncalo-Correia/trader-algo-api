using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class BindTradeBotsToMlPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MlPolicyId",
                table: "trade_bots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TakeProfit",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "StopLoss",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Slippage",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxTrailingDrawdown",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MaxCandlesPerTrade",
                table: "ml_policies",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Fee",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DailyProfit",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DailyDrawdownLimit",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BreakevenStop",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Breakeven",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_trade_bots_MlPolicyId",
                table: "trade_bots",
                column: "MlPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_trade_bots_ml_policies_MlPolicyId",
                table: "trade_bots",
                column: "MlPolicyId",
                principalTable: "ml_policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trade_bots_ml_policies_MlPolicyId",
                table: "trade_bots");

            migrationBuilder.DropIndex(
                name: "IX_trade_bots_MlPolicyId",
                table: "trade_bots");

            migrationBuilder.DropColumn(
                name: "MlPolicyId",
                table: "trade_bots");

            migrationBuilder.AlterColumn<decimal>(
                name: "TakeProfit",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "StopLoss",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "Slippage",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxTrailingDrawdown",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<int>(
                name: "MaxCandlesPerTrade",
                table: "ml_policies",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "Fee",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "DailyProfit",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "DailyDrawdownLimit",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "BreakevenStop",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "Breakeven",
                table: "ml_policies",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);
        }
    }
}
