using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class RenameTradingAccountBalanceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartingAmount",
                table: "trading_accounts",
                newName: "InitialBalance");

            migrationBuilder.RenameColumn(
                name: "CurrentAmount",
                table: "trading_accounts",
                newName: "CurrentBalance");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "trading_accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                table: "trading_accounts",
                columns: new[] { "Id", "CreatedAt", "CurrentBalance", "InitialBalance", "IsActive", "Name", "TradingStrategyId" },
                values: new object[] { 1L, new DateTimeOffset(new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1000m, 1000m, true, "Default", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "trading_accounts",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DropColumn(
                name: "Name",
                table: "trading_accounts");

            migrationBuilder.RenameColumn(
                name: "InitialBalance",
                table: "trading_accounts",
                newName: "StartingAmount");

            migrationBuilder.RenameColumn(
                name: "CurrentBalance",
                table: "trading_accounts",
                newName: "CurrentAmount");
        }
    }
}
