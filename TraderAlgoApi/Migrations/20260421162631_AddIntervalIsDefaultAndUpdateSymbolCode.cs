using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddIntervalIsDefaultAndUpdateSymbolCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "intervals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "intervals",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsDefault",
                value: false);

            migrationBuilder.UpdateData(
                table: "intervals",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsDefault",
                value: false);

            migrationBuilder.UpdateData(
                table: "intervals",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsDefault",
                value: false);

            migrationBuilder.UpdateData(
                table: "intervals",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "intervals",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsDefault",
                value: false);

            migrationBuilder.UpdateData(
                table: "intervals",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsDefault",
                value: false);

            migrationBuilder.UpdateData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Code", "DisplayName", "QuoteAsset" },
                values: new object[] { "BTCUSDT", "BTC/USDT", "USDT" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "intervals");

            migrationBuilder.UpdateData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Code", "DisplayName", "QuoteAsset" },
                values: new object[] { "BTCUSD", "BTC/USD", "USD" });
        }
    }
}
