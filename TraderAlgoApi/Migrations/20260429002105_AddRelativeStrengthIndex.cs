using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddRelativeStrengthIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "relative_strength_indexes",
                columns: table => new
                {
                    KlineDataId = table.Column<long>(type: "bigint", nullable: false),
                    rsi = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    rsi_smooth = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    divergence = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relative_strength_indexes", x => x.KlineDataId);
                    table.ForeignKey(
                        name: "FK_relative_strength_indexes_kline_data_KlineDataId",
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
                name: "relative_strength_indexes");
        }
    }
}
