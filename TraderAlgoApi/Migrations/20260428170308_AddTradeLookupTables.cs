using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trades_SymbolCode_Status",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "CloseReason",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "trades");

            migrationBuilder.AddColumn<int>(
                name: "CloseReasonId",
                table: "trades",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderTypeId",
                table: "trades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SideId",
                table: "trades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "trades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "trade_close_reasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_close_reasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trade_order_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_order_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trade_sides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_sides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trade_statuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_statuses", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "trade_close_reasons",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Manual" },
                    { 2, "StopLoss" },
                    { 3, "TakeProfit" }
                });

            migrationBuilder.InsertData(
                table: "trade_order_types",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Market" },
                    { 2, "Limit" }
                });

            migrationBuilder.InsertData(
                table: "trade_sides",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Buy" },
                    { 2, "Sell" }
                });

            migrationBuilder.InsertData(
                table: "trade_statuses",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Active" },
                    { 3, "Closed" },
                    { 4, "Cancelled" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_trades_CloseReasonId",
                table: "trades",
                column: "CloseReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_trades_OrderTypeId",
                table: "trades",
                column: "OrderTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_trades_SideId",
                table: "trades",
                column: "SideId");

            migrationBuilder.CreateIndex(
                name: "IX_trades_StatusId",
                table: "trades",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_trades_SymbolCode_StatusId",
                table: "trades",
                columns: new[] { "SymbolCode", "StatusId" });

            migrationBuilder.AddForeignKey(
                name: "FK_trades_trade_close_reasons_CloseReasonId",
                table: "trades",
                column: "CloseReasonId",
                principalTable: "trade_close_reasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trades_trade_order_types_OrderTypeId",
                table: "trades",
                column: "OrderTypeId",
                principalTable: "trade_order_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trades_trade_sides_SideId",
                table: "trades",
                column: "SideId",
                principalTable: "trade_sides",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trades_trade_statuses_StatusId",
                table: "trades",
                column: "StatusId",
                principalTable: "trade_statuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trades_trade_close_reasons_CloseReasonId",
                table: "trades");

            migrationBuilder.DropForeignKey(
                name: "FK_trades_trade_order_types_OrderTypeId",
                table: "trades");

            migrationBuilder.DropForeignKey(
                name: "FK_trades_trade_sides_SideId",
                table: "trades");

            migrationBuilder.DropForeignKey(
                name: "FK_trades_trade_statuses_StatusId",
                table: "trades");

            migrationBuilder.DropTable(
                name: "trade_close_reasons");

            migrationBuilder.DropTable(
                name: "trade_order_types");

            migrationBuilder.DropTable(
                name: "trade_sides");

            migrationBuilder.DropTable(
                name: "trade_statuses");

            migrationBuilder.DropIndex(
                name: "IX_trades_CloseReasonId",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "IX_trades_OrderTypeId",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "IX_trades_SideId",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "IX_trades_StatusId",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "IX_trades_SymbolCode_StatusId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "CloseReasonId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "OrderTypeId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "SideId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "trades");

            migrationBuilder.AddColumn<string>(
                name: "CloseReason",
                table: "trades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderType",
                table: "trades",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Side",
                table: "trades",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "trades",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_trades_SymbolCode_Status",
                table: "trades",
                columns: new[] { "SymbolCode", "Status" });
        }
    }
}
