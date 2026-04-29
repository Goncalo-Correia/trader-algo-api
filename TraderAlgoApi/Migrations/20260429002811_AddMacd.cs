using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMacd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "macd",
                columns: table => new
                {
                    KlineDataId = table.Column<long>(type: "bigint", nullable: false),
                    macd_line = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    signal_line = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    histogram = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_macd", x => x.KlineDataId);
                    table.ForeignKey(
                        name: "FK_macd_kline_data_KlineDataId",
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
                name: "macd");
        }
    }
}
