using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddKlineDataIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kline_data_indicators",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KlineDataId = table.Column<long>(type: "bigint", nullable: false),
                    Sma20 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Sma100 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kline_data_indicators");
        }
    }
}
