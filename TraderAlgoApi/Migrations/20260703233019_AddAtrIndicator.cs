using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAtrIndicator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "atr",
                columns: table => new
                {
                    KlineDataId = table.Column<long>(type: "bigint", nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    true_range = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    atr = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_atr", x => x.KlineDataId);
                    table.ForeignKey(
                        name: "FK_atr_kline_data_KlineDataId",
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
                name: "atr");
        }
    }
}
