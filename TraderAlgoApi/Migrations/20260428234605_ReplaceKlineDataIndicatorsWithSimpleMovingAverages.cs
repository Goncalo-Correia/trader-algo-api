using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceKlineDataIndicatorsWithSimpleMovingAverages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kline_data_indicators");

            migrationBuilder.CreateTable(
                name: "simple_moving_averages",
                columns: table => new
                {
                    KlineDataId = table.Column<long>(type: "bigint", nullable: false),
                    sma_20 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    sma_100 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simple_moving_averages", x => x.KlineDataId);
                    table.ForeignKey(
                        name: "FK_simple_moving_averages_kline_data_KlineDataId",
                        column: x => x.KlineDataId,
                        principalTable: "kline_data",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "simple_moving_averages");

            migrationBuilder.CreateTable(
                name: "kline_data_indicators",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KlineDataId = table.Column<long>(type: "bigint", nullable: false),
                    Sma100 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Sma20 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kline_data_indicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kline_data_indicators_kline_data_KlineDataId",
                        column: x => x.KlineDataId,
                        principalTable: "kline_data",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_kline_data_indicators_KlineDataId",
                table: "kline_data_indicators",
                column: "KlineDataId",
                unique: true);
        }
    }
}
