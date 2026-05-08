using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSymbolProviderAndSpy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "symbols",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 1,
                column: "Provider",
                value: 0);

            migrationBuilder.InsertData(
                table: "symbols",
                columns: new[] { "Id", "BaseAsset", "Code", "CreatedAt", "DisplayName", "IsActive", "IsDefault", "Provider", "QuoteAsset" },
                values: new object[] { 2, "SPY", "SPY", new DateTimeOffset(new DateTime(2026, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SPY", true, false, 1, "USD" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "symbols");
        }
    }
}
