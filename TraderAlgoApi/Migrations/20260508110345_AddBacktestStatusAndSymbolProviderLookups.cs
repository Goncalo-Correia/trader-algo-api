using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBacktestStatusAndSymbolProviderLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Provider",
                table: "symbols",
                newName: "ProviderId");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "backtests",
                newName: "StatusId");

            migrationBuilder.CreateTable(
                name: "backtest_statuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backtest_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "symbol_providers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbol_providers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "backtest_statuses",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Running" },
                    { 3, "Completed" },
                    { 4, "Failed" },
                    { 5, "Cancelled" }
                });

            migrationBuilder.InsertData(
                table: "symbol_providers",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Binance" },
                    { 2, "Alpaca" }
                });

            // Shift existing 0-based enum values to the new 1-based lookup IDs.
            migrationBuilder.Sql("UPDATE symbols SET \"ProviderId\" = \"ProviderId\" + 1;");
            migrationBuilder.Sql("UPDATE backtests SET \"StatusId\" = \"StatusId\" + 1;");

            migrationBuilder.UpdateData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 1,
                column: "ProviderId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 2,
                column: "ProviderId",
                value: 2);

            migrationBuilder.CreateIndex(
                name: "IX_symbols_ProviderId",
                table: "symbols",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_backtests_StatusId",
                table: "backtests",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_backtests_backtest_statuses_StatusId",
                table: "backtests",
                column: "StatusId",
                principalTable: "backtest_statuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_symbols_symbol_providers_ProviderId",
                table: "symbols",
                column: "ProviderId",
                principalTable: "symbol_providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_backtests_backtest_statuses_StatusId",
                table: "backtests");

            migrationBuilder.DropForeignKey(
                name: "FK_symbols_symbol_providers_ProviderId",
                table: "symbols");

            migrationBuilder.DropTable(
                name: "backtest_statuses");

            migrationBuilder.DropTable(
                name: "symbol_providers");

            migrationBuilder.DropIndex(
                name: "IX_symbols_ProviderId",
                table: "symbols");

            migrationBuilder.DropIndex(
                name: "IX_backtests_StatusId",
                table: "backtests");

            migrationBuilder.RenameColumn(
                name: "ProviderId",
                table: "symbols",
                newName: "Provider");

            migrationBuilder.RenameColumn(
                name: "StatusId",
                table: "backtests",
                newName: "Status");

            // Restore 1-based lookup IDs back to 0-based enum values.
            migrationBuilder.Sql("UPDATE symbols SET \"Provider\" = \"Provider\" - 1;");
            migrationBuilder.Sql("UPDATE backtests SET \"Status\" = \"Status\" - 1;");

            migrationBuilder.UpdateData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 1,
                column: "Provider",
                value: 0);

            migrationBuilder.UpdateData(
                table: "symbols",
                keyColumn: "Id",
                keyValue: 2,
                column: "Provider",
                value: 1);
        }
    }
}
