using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBreakevenToBacktest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Breakeven",
                table: "backtests",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Breakeven",
                table: "backtests");
        }
    }
}
