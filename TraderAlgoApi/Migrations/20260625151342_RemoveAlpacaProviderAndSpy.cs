using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAlpacaProviderAndSpy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "symbol_providers",
                keyColumn: "Id",
                keyValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "symbol_providers",
                columns: new[] { "Id", "Name" },
                values: new object[] { 2, "Alpaca" });

            migrationBuilder.InsertData(
                table: "symbols",
                columns: new[] { "Id", "BaseAsset", "Code", "CreatedAt", "DisplayName", "IsActive", "IsDefault", "ProviderId", "QuoteAsset" },
                values: new object[] { 2, "SPY", "SPY", new DateTimeOffset(new DateTime(2026, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SPY", true, false, 2, "USD" });
        }
    }
}
