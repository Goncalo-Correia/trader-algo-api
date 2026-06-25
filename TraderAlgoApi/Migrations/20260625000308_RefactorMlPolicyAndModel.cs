using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class RefactorMlPolicyAndModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ml_training_runs_intervals_IntervalId",
                table: "ml_training_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_ml_training_runs_symbols_SymbolId",
                table: "ml_training_runs");

            migrationBuilder.DropIndex(
                name: "IX_ml_training_runs_IntervalId",
                table: "ml_training_runs");

            migrationBuilder.DropIndex(
                name: "IX_ml_training_runs_ModelId",
                table: "ml_training_runs");

            migrationBuilder.DropIndex(
                name: "IX_ml_training_runs_SymbolId",
                table: "ml_training_runs");

            migrationBuilder.DropColumn(
                name: "IntervalId",
                table: "ml_training_runs");

            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "ml_training_runs");

            migrationBuilder.DropColumn(
                name: "SymbolId",
                table: "ml_training_runs");

            migrationBuilder.DropColumn(
                name: "TotalTimesteps",
                table: "ml_training_runs");

            migrationBuilder.AddColumn<long>(
                name: "MlPolicyId",
                table: "ml_training_runs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ml_models",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ml_policies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelId = table.Column<int>(type: "integer", nullable: false),
                    SymbolId = table.Column<int>(type: "integer", nullable: false),
                    IntervalId = table.Column<int>(type: "integer", nullable: false),
                    TotalTimesteps = table.Column<int>(type: "integer", nullable: false),
                    InitialBalance = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    StopLoss = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Breakeven = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    BreakevenStop = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Fee = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Slippage = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    DailyProfit = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    DailyDrawdownLimit = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    MaxCandlesPerTrade = table.Column<int>(type: "integer", nullable: true),
                    MaxTrailingDrawdown = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ml_policies_intervals_IntervalId",
                        column: x => x.IntervalId,
                        principalTable: "intervals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ml_policies_ml_models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "ml_models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ml_policies_symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ml_models",
                columns: new[] { "Id", "Name" },
                values: new object[] { 1, "ppo-v1" });

            migrationBuilder.CreateIndex(
                name: "IX_ml_training_runs_MlPolicyId",
                table: "ml_training_runs",
                column: "MlPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_models_Name",
                table: "ml_models",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ml_policies_IntervalId",
                table: "ml_policies",
                column: "IntervalId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_policies_ModelId",
                table: "ml_policies",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_policies_SymbolId",
                table: "ml_policies",
                column: "SymbolId");

            migrationBuilder.AddForeignKey(
                name: "FK_ml_training_runs_ml_policies_MlPolicyId",
                table: "ml_training_runs",
                column: "MlPolicyId",
                principalTable: "ml_policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ml_training_runs_ml_policies_MlPolicyId",
                table: "ml_training_runs");

            migrationBuilder.DropTable(
                name: "ml_policies");

            migrationBuilder.DropTable(
                name: "ml_models");

            migrationBuilder.DropIndex(
                name: "IX_ml_training_runs_MlPolicyId",
                table: "ml_training_runs");

            migrationBuilder.DropColumn(
                name: "MlPolicyId",
                table: "ml_training_runs");

            migrationBuilder.AddColumn<int>(
                name: "IntervalId",
                table: "ml_training_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ModelId",
                table: "ml_training_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SymbolId",
                table: "ml_training_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalTimesteps",
                table: "ml_training_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ml_training_runs_IntervalId",
                table: "ml_training_runs",
                column: "IntervalId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_training_runs_ModelId",
                table: "ml_training_runs",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_training_runs_SymbolId",
                table: "ml_training_runs",
                column: "SymbolId");

            migrationBuilder.AddForeignKey(
                name: "FK_ml_training_runs_intervals_IntervalId",
                table: "ml_training_runs",
                column: "IntervalId",
                principalTable: "intervals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ml_training_runs_symbols_SymbolId",
                table: "ml_training_runs",
                column: "SymbolId",
                principalTable: "symbols",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
