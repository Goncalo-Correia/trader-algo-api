using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryTablesToPublicSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "training_chart_artifacts",
                columns: table => new
                {
                    run_id = table.Column<string>(type: "text", nullable: false),
                    chart_key = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: true),
                    storage_path = table.Column<string>(type: "text", nullable: true),
                    content_type = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_chart_artifacts", x => new { x.run_id, x.chart_key });
                });

            migrationBuilder.CreateTable(
                name: "training_checkpoint_evals",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<string>(type: "text", nullable: true),
                    timesteps = table.Column<int>(type: "integer", nullable: true),
                    train_eval_r = table.Column<double>(type: "double precision", nullable: true),
                    val_r = table.Column<double>(type: "double precision", nullable: true),
                    train_dd_pct = table.Column<double>(type: "double precision", nullable: true),
                    val_dd_pct = table.Column<double>(type: "double precision", nullable: true),
                    q_train = table.Column<double>(type: "double precision", nullable: true),
                    q_val = table.Column<double>(type: "double precision", nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    gap = table.Column<double>(type: "double precision", nullable: true),
                    eligible = table.Column<bool>(type: "boolean", nullable: true),
                    is_best = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_checkpoint_evals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_equity_points",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<string>(type: "text", nullable: true),
                    split = table.Column<string>(type: "text", nullable: true),
                    ts = table.Column<long>(type: "bigint", nullable: true),
                    equity = table.Column<double>(type: "double precision", nullable: true),
                    drawdown_pct = table.Column<double>(type: "double precision", nullable: true),
                    realized_pnl = table.Column<double>(type: "double precision", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_equity_points", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_feature_quality",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<string>(type: "text", nullable: true),
                    feature = table.Column<string>(type: "text", nullable: true),
                    mean = table.Column<double>(type: "double precision", nullable: true),
                    std = table.Column<double>(type: "double precision", nullable: true),
                    skew = table.Column<double>(type: "double precision", nullable: true),
                    excess_kurt = table.Column<double>(type: "double precision", nullable: true),
                    cv = table.Column<double>(type: "double precision", nullable: true),
                    spearman_r_1bar = table.Column<double>(type: "double precision", nullable: true),
                    spearman_p_1bar = table.Column<double>(type: "double precision", nullable: true),
                    signal_p05 = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_feature_quality", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_fold_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<string>(type: "text", nullable: true),
                    fold = table.Column<int>(type: "integer", nullable: true),
                    scheme = table.Column<string>(type: "text", nullable: true),
                    is_oos = table.Column<bool>(type: "boolean", nullable: true),
                    train_start = table.Column<string>(type: "text", nullable: true),
                    train_end = table.Column<string>(type: "text", nullable: true),
                    val_start = table.Column<string>(type: "text", nullable: true),
                    val_end = table.Column<string>(type: "text", nullable: true),
                    test_start = table.Column<string>(type: "text", nullable: true),
                    test_end = table.Column<string>(type: "text", nullable: true),
                    return_pct = table.Column<double>(type: "double precision", nullable: true),
                    sharpe = table.Column<double>(type: "double precision", nullable: true),
                    profit_factor = table.Column<double>(type: "double precision", nullable: true),
                    win_rate_pct = table.Column<double>(type: "double precision", nullable: true),
                    max_dd_pct = table.Column<double>(type: "double precision", nullable: true),
                    avg_r = table.Column<double>(type: "double precision", nullable: true),
                    n_trades = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_fold_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_learning_curve",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<string>(type: "text", nullable: true),
                    timesteps = table.Column<int>(type: "integer", nullable: true),
                    mean_ep_reward = table.Column<double>(type: "double precision", nullable: true),
                    std_ep_reward = table.Column<double>(type: "double precision", nullable: true),
                    mean_ep_length = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_learning_curve", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_run_performance",
                columns: table => new
                {
                    run_id = table.Column<string>(type: "text", nullable: false),
                    ml_policy_id = table.Column<int>(type: "integer", nullable: true),
                    scheme = table.Column<string>(type: "text", nullable: true),
                    from_date = table.Column<string>(type: "text", nullable: true),
                    to_date = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    promoted = table.Column<bool>(type: "boolean", nullable: true),
                    gate_passed = table.Column<bool>(type: "boolean", nullable: true),
                    gate_detail = table.Column<string>(type: "jsonb", nullable: true, defaultValueSql: "'{}'::jsonb"),
                    seed = table.Column<int>(type: "integer", nullable: true),
                    obs_dim = table.Column<int>(type: "integer", nullable: true),
                    schema_version = table.Column<int>(type: "integer", nullable: true),
                    in_sample_pnl_pct = table.Column<double>(type: "double precision", nullable: true),
                    oos_pnl_pct = table.Column<double>(type: "double precision", nullable: true),
                    oos_sharpe = table.Column<double>(type: "double precision", nullable: true),
                    oos_profit_factor = table.Column<double>(type: "double precision", nullable: true),
                    oos_max_dd_pct = table.Column<double>(type: "double precision", nullable: true),
                    in_sample_minus_oos_pnl_pct = table.Column<double>(type: "double precision", nullable: true),
                    n_folds = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_run_performance", x => x.run_id);
                });

            migrationBuilder.CreateTable(
                name: "training_split_metrics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<string>(type: "text", nullable: true),
                    split = table.Column<string>(type: "text", nullable: true),
                    total_return_pct = table.Column<double>(type: "double precision", nullable: true),
                    annualized_return_pct = table.Column<double>(type: "double precision", nullable: true),
                    max_drawdown_pct = table.Column<double>(type: "double precision", nullable: true),
                    sharpe_like = table.Column<double>(type: "double precision", nullable: true),
                    sortino_ratio = table.Column<double>(type: "double precision", nullable: true),
                    calmar_ratio = table.Column<double>(type: "double precision", nullable: true),
                    profit_factor = table.Column<double>(type: "double precision", nullable: true),
                    win_rate_pct = table.Column<double>(type: "double precision", nullable: true),
                    avg_r = table.Column<double>(type: "double precision", nullable: true),
                    n_trades = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_split_metrics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_trades",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<string>(type: "text", nullable: true),
                    split = table.Column<string>(type: "text", nullable: true),
                    entry_time = table.Column<long>(type: "bigint", nullable: true),
                    exit_time = table.Column<long>(type: "bigint", nullable: true),
                    direction = table.Column<string>(type: "text", nullable: true),
                    entry_price = table.Column<double>(type: "double precision", nullable: true),
                    exit_price = table.Column<double>(type: "double precision", nullable: true),
                    sl = table.Column<double>(type: "double precision", nullable: true),
                    tp = table.Column<double>(type: "double precision", nullable: true),
                    sl_atr_mult = table.Column<double>(type: "double precision", nullable: true),
                    tp_r_bracket = table.Column<double>(type: "double precision", nullable: true),
                    units = table.Column<double>(type: "double precision", nullable: true),
                    pnl = table.Column<double>(type: "double precision", nullable: true),
                    r_mult = table.Column<double>(type: "double precision", nullable: true),
                    bars_in_trade = table.Column<int>(type: "integer", nullable: true),
                    exit_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_trades", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_training_checkpoint_evals_run_id_timesteps",
                table: "training_checkpoint_evals",
                columns: new[] { "run_id", "timesteps" });

            migrationBuilder.CreateIndex(
                name: "IX_training_equity_points_run_id_split_ts",
                table: "training_equity_points",
                columns: new[] { "run_id", "split", "ts" });

            migrationBuilder.CreateIndex(
                name: "IX_training_feature_quality_run_id_feature",
                table: "training_feature_quality",
                columns: new[] { "run_id", "feature" });

            migrationBuilder.CreateIndex(
                name: "IX_training_fold_results_run_id_fold",
                table: "training_fold_results",
                columns: new[] { "run_id", "fold" });

            migrationBuilder.CreateIndex(
                name: "IX_training_learning_curve_run_id_timesteps",
                table: "training_learning_curve",
                columns: new[] { "run_id", "timesteps" });

            migrationBuilder.CreateIndex(
                name: "IX_training_run_performance_ml_policy_id_created_at",
                table: "training_run_performance",
                columns: new[] { "ml_policy_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_training_split_metrics_run_id_split",
                table: "training_split_metrics",
                columns: new[] { "run_id", "split" });

            migrationBuilder.CreateIndex(
                name: "IX_training_trades_run_id_split_entry_time",
                table: "training_trades",
                columns: new[] { "run_id", "split", "entry_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "training_chart_artifacts");

            migrationBuilder.DropTable(
                name: "training_checkpoint_evals");

            migrationBuilder.DropTable(
                name: "training_equity_points");

            migrationBuilder.DropTable(
                name: "training_feature_quality");

            migrationBuilder.DropTable(
                name: "training_fold_results");

            migrationBuilder.DropTable(
                name: "training_learning_curve");

            migrationBuilder.DropTable(
                name: "training_run_performance");

            migrationBuilder.DropTable(
                name: "training_split_metrics");

            migrationBuilder.DropTable(
                name: "training_trades");
        }
    }
}
