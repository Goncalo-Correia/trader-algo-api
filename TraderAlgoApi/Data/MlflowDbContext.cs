using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models.Ml;

namespace TraderAlgoApi.Data;

/// <summary>
/// Read-only projection of the MLflow tracking store (the <c>mlflow</c> Postgres schema, owned and
/// migrated by MLflow itself — NOT by EF). Only the tables this API actually reads are mapped:
/// core tracking (experiments/runs/params/metrics/latest_metrics/tags), the model registry
/// (registered_models/model_versions + their alias/tag satellites), and MLflow infra
/// (experiment_tags, workspaces, alembic_version). The dozens of MLflow feature tables this project
/// never uses (tracing, evaluation/review, serving/gateway, guardrails, webhooks, jobs, logged
/// models, datasets/inputs, …) are intentionally not modelled.
/// </summary>
public sealed class MlflowDbContext(DbContextOptions<MlflowDbContext> options) : DbContext(options)
{
    // ── Core tracking ──────────────────────────────────────────────────────────
    public DbSet<MlflowExperiment>     Experiments     => Set<MlflowExperiment>();
    public DbSet<MlflowRun>            Runs            => Set<MlflowRun>();
    public DbSet<MlflowTag>            Tags            => Set<MlflowTag>();
    public DbSet<MlflowMetric>         Metrics         => Set<MlflowMetric>();
    public DbSet<MlflowLatestMetric>   LatestMetrics   => Set<MlflowLatestMetric>();
    public DbSet<MlflowParam>          Params          => Set<MlflowParam>();
    public DbSet<MlflowExperimentTag>  ExperimentTags  => Set<MlflowExperimentTag>();
    public DbSet<MlflowAlembicVersion> AlembicVersions => Set<MlflowAlembicVersion>();
    public DbSet<MlflowWorkspace>      Workspaces      => Set<MlflowWorkspace>();

    // ── Model registry ─────────────────────────────────────────────────────────
    public DbSet<MlflowRegisteredModel>      RegisteredModels       => Set<MlflowRegisteredModel>();
    public DbSet<MlflowModelVersion>         ModelVersions          => Set<MlflowModelVersion>();
    public DbSet<MlflowRegisteredModelTag>   RegisteredModelTags    => Set<MlflowRegisteredModelTag>();
    public DbSet<MlflowModelVersionTag>      ModelVersionTags       => Set<MlflowModelVersionTag>();
    public DbSet<MlflowRegisteredModelAlias> RegisteredModelAliases => Set<MlflowRegisteredModelAlias>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Core tracking ──────────────────────────────────────────────────────
        modelBuilder.Entity<MlflowTag>()
            .HasKey(e => new { e.Key, e.RunUuid });

        modelBuilder.Entity<MlflowMetric>()
            .HasKey(e => new { e.Key, e.Value, e.Timestamp, e.RunUuid, e.Step, e.IsNan });

        modelBuilder.Entity<MlflowParam>()
            .HasKey(e => new { e.Key, e.RunUuid });

        modelBuilder.Entity<MlflowExperimentTag>()
            .HasKey(e => new { e.Key, e.ExperimentId });

        modelBuilder.Entity<MlflowLatestMetric>()
            .HasKey(e => new { e.Key, e.RunUuid });

        // ── Model registry ─────────────────────────────────────────────────────
        modelBuilder.Entity<MlflowRegisteredModel>()
            .HasKey(e => new { e.Name, e.Workspace });

        modelBuilder.Entity<MlflowModelVersion>()
            .HasKey(e => new { e.Name, e.Version, e.Workspace });

        modelBuilder.Entity<MlflowRegisteredModelTag>()
            .HasKey(e => new { e.Key, e.Name, e.Workspace });

        modelBuilder.Entity<MlflowModelVersionTag>()
            .HasKey(e => new { e.Key, e.Name, e.Version, e.Workspace });

        modelBuilder.Entity<MlflowRegisteredModelAlias>()
            .HasKey(e => new { e.Alias, e.Name, e.Workspace });
    }
}
