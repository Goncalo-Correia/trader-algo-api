using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMlTrainingRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ml_training_run_statuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_training_run_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ml_training_runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SymbolId = table.Column<int>(type: "integer", nullable: false),
                    IntervalId = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    TotalTimesteps = table.Column<int>(type: "integer", nullable: true),
                    FinalBalance = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    PnlPct = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    NTrades = table.Column<int>(type: "integer", nullable: true),
                    RunId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_training_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ml_training_runs_intervals_IntervalId",
                        column: x => x.IntervalId,
                        principalTable: "intervals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ml_training_runs_ml_training_run_statuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "ml_training_run_statuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ml_training_runs_symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ml_training_run_statuses",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Running" },
                    { 3, "Completed" },
                    { 4, "Failed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ml_training_runs_IntervalId",
                table: "ml_training_runs",
                column: "IntervalId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_training_runs_ModelId",
                table: "ml_training_runs",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_training_runs_StatusId",
                table: "ml_training_runs",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_training_runs_SymbolId",
                table: "ml_training_runs",
                column: "SymbolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ml_training_runs");

            migrationBuilder.DropTable(
                name: "ml_training_run_statuses");
        }
    }
}
