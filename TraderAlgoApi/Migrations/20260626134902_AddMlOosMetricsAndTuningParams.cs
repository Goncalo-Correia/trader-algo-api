using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMlOosMetricsAndTuningParams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FinalBalanceOos",
                table: "ml_training_runs",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PnlPctOos",
                table: "ml_training_runs",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchSize",
                table: "ml_policies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ClipRange",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EntCoef",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EntryCost",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EpisodeDays",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GaeLambda",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Gamma",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LearningRate",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxPatienceRewardPerDay",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxStreakBonus",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NEpochs",
                table: "ml_policies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NSteps",
                table: "ml_policies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NoTradeDayPenalty",
                table: "ml_policies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OosEvalEvery",
                table: "ml_policies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StreakBonusCoef",
                table: "ml_policies",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalBalanceOos",
                table: "ml_training_runs");

            migrationBuilder.DropColumn(
                name: "PnlPctOos",
                table: "ml_training_runs");

            migrationBuilder.DropColumn(
                name: "BatchSize",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "ClipRange",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "EntCoef",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "EntryCost",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "EpisodeDays",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "GaeLambda",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "Gamma",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "LearningRate",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "MaxPatienceRewardPerDay",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "MaxStreakBonus",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "NEpochs",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "NSteps",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "NoTradeDayPenalty",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "OosEvalEvery",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "StreakBonusCoef",
                table: "ml_policies");
        }
    }
}
