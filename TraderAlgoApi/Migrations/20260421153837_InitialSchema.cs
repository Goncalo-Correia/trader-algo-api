using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intervals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intervals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "symbols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseAsset = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    QuoteAsset = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbols", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "kline_data",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SymbolId = table.Column<int>(type: "integer", nullable: false),
                    IntervalId = table.Column<int>(type: "integer", nullable: false),
                    OpenTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CloseTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Open = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    High = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Low = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Close = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    QuoteAssetVolume = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    NumberOfTrades = table.Column<int>(type: "integer", nullable: false),
                    TakerBuyBaseAssetVolume = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    TakerBuyQuoteAssetVolume = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kline_data", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kline_data_intervals_IntervalId",
                        column: x => x.IntervalId,
                        principalTable: "intervals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kline_data_symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "intervals",
                columns: new[] { "Id", "Code", "CreatedAt", "DisplayName", "Duration", "IsActive" },
                values: new object[,]
                {
                    { 1, "1m", new DateTimeOffset(new DateTime(2026, 4, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "1 Minute", new TimeSpan(0, 0, 1, 0, 0), true },
                    { 2, "5m", new DateTimeOffset(new DateTime(2026, 4, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "5 Minute", new TimeSpan(0, 0, 5, 0, 0), true },
                    { 3, "15m", new DateTimeOffset(new DateTime(2026, 4, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "15 Minute", new TimeSpan(0, 0, 15, 0, 0), true },
                    { 4, "1h", new DateTimeOffset(new DateTime(2026, 4, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "1H", new TimeSpan(0, 1, 0, 0, 0), true },
                    { 5, "4h", new DateTimeOffset(new DateTime(2026, 4, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "4H", new TimeSpan(0, 4, 0, 0, 0), true },
                    { 6, "1d", new DateTimeOffset(new DateTime(2026, 4, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "1D", new TimeSpan(1, 0, 0, 0, 0), true }
                });

            migrationBuilder.InsertData(
                table: "symbols",
                columns: new[] { "Id", "BaseAsset", "Code", "CreatedAt", "DisplayName", "IsActive", "QuoteAsset" },
                values: new object[] { 1, "BTC", "BTCUSD", new DateTimeOffset(new DateTime(2026, 4, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "BTC/USD", true, "USD" });

            migrationBuilder.CreateIndex(
                name: "IX_intervals_Code",
                table: "intervals",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kline_data_IntervalId",
                table: "kline_data",
                column: "IntervalId");

            migrationBuilder.CreateIndex(
                name: "IX_kline_data_SymbolId_IntervalId_OpenTime",
                table: "kline_data",
                columns: new[] { "SymbolId", "IntervalId", "OpenTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_symbols_Code",
                table: "symbols",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kline_data");

            migrationBuilder.DropTable(
                name: "intervals");

            migrationBuilder.DropTable(
                name: "symbols");
        }
    }
}
