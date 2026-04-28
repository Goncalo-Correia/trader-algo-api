using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class TradeSymbolIntervalForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trades_SymbolCode_StatusId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "IntervalCode",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "SymbolCode",
                table: "trades");

            migrationBuilder.AddColumn<int>(
                name: "IntervalId",
                table: "trades",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SymbolId",
                table: "trades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_trades_IntervalId",
                table: "trades",
                column: "IntervalId");

            migrationBuilder.CreateIndex(
                name: "IX_trades_SymbolId_StatusId",
                table: "trades",
                columns: new[] { "SymbolId", "StatusId" });

            migrationBuilder.AddForeignKey(
                name: "FK_trades_intervals_IntervalId",
                table: "trades",
                column: "IntervalId",
                principalTable: "intervals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trades_symbols_SymbolId",
                table: "trades",
                column: "SymbolId",
                principalTable: "symbols",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trades_intervals_IntervalId",
                table: "trades");

            migrationBuilder.DropForeignKey(
                name: "FK_trades_symbols_SymbolId",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "IX_trades_IntervalId",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "IX_trades_SymbolId_StatusId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "IntervalId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "SymbolId",
                table: "trades");

            migrationBuilder.AddColumn<string>(
                name: "IntervalCode",
                table: "trades",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SymbolCode",
                table: "trades",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_trades_SymbolCode_StatusId",
                table: "trades",
                columns: new[] { "SymbolCode", "StatusId" });
        }
    }
}
