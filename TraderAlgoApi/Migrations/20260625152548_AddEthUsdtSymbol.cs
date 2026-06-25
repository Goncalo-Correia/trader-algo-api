using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEthUsdtSymbol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "symbols",
                columns: new[] { "Id", "BaseAsset", "Code", "CreatedAt", "DisplayName", "IsActive", "IsDefault", "ProviderId", "QuoteAsset" },
                values: new object[] { 3, "ETH", "ETHUSDT", new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ETH/USDT", true, false, 1, "USDT" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
