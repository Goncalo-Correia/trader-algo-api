using Microsoft.EntityFrameworkCore;
using TraderAlgoApi.Models.Ml;

namespace TraderAlgoApi.Data;

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
    public DbSet<MlflowDataset>        Datasets        => Set<MlflowDataset>();
    public DbSet<MlflowInput>          Inputs          => Set<MlflowInput>();
    public DbSet<MlflowInputTag>       InputTags       => Set<MlflowInputTag>();

    // ── Model registry ─────────────────────────────────────────────────────────
    public DbSet<MlflowRegisteredModel>      RegisteredModels      => Set<MlflowRegisteredModel>();
    public DbSet<MlflowModelVersion>         ModelVersions         => Set<MlflowModelVersion>();
    public DbSet<MlflowRegisteredModelTag>   RegisteredModelTags   => Set<MlflowRegisteredModelTag>();
    public DbSet<MlflowModelVersionTag>      ModelVersionTags      => Set<MlflowModelVersionTag>();
    public DbSet<MlflowRegisteredModelAlias> RegisteredModelAliases => Set<MlflowRegisteredModelAlias>();
    public DbSet<MlflowLoggedModel>          LoggedModels          => Set<MlflowLoggedModel>();
    public DbSet<MlflowLoggedModelMetric>    LoggedModelMetrics    => Set<MlflowLoggedModelMetric>();
    public DbSet<MlflowLoggedModelParam>     LoggedModelParams     => Set<MlflowLoggedModelParam>();
    public DbSet<MlflowLoggedModelTag>       LoggedModelTags       => Set<MlflowLoggedModelTag>();

    // ── Tracing ────────────────────────────────────────────────────────────────
    public DbSet<MlflowTraceInfo>            TraceInfos            => Set<MlflowTraceInfo>();
    public DbSet<MlflowTraceTag>             TraceTags             => Set<MlflowTraceTag>();
    public DbSet<MlflowTraceRequestMetadata> TraceRequestMetadata  => Set<MlflowTraceRequestMetadata>();
    public DbSet<MlflowTraceMetric>          TraceMetrics          => Set<MlflowTraceMetric>();
    public DbSet<MlflowSpan>                 Spans                 => Set<MlflowSpan>();
    public DbSet<MlflowSpanMetric>           SpanMetrics           => Set<MlflowSpanMetric>();
    public DbSet<MlflowAssessment>           Assessments           => Set<MlflowAssessment>();

    // ── Evaluation & review ────────────────────────────────────────────────────
    public DbSet<MlflowEvaluationDataset>       EvaluationDatasets      => Set<MlflowEvaluationDataset>();
    public DbSet<MlflowEvaluationDatasetTag>    EvaluationDatasetTags   => Set<MlflowEvaluationDatasetTag>();
    public DbSet<MlflowEvaluationDatasetRecord> EvaluationDatasetRecords => Set<MlflowEvaluationDatasetRecord>();
    public DbSet<MlflowLabelSchema>             LabelSchemas            => Set<MlflowLabelSchema>();
    public DbSet<MlflowReviewQueue>             ReviewQueues            => Set<MlflowReviewQueue>();
    public DbSet<MlflowReviewQueueUser>         ReviewQueueUsers        => Set<MlflowReviewQueueUser>();
    public DbSet<MlflowReviewQueueItem>         ReviewQueueItems        => Set<MlflowReviewQueueItem>();
    public DbSet<MlflowReviewQueueLabelSchema>  ReviewQueueLabelSchemas => Set<MlflowReviewQueueLabelSchema>();

    // ── Scoring & guardrails ───────────────────────────────────────────────────
    public DbSet<MlflowScorer>             Scorers             => Set<MlflowScorer>();
    public DbSet<MlflowScorerVersion>      ScorerVersions      => Set<MlflowScorerVersion>();
    public DbSet<MlflowOnlineScoringConfig> OnlineScoringConfigs => Set<MlflowOnlineScoringConfig>();
    public DbSet<MlflowGuardrail>          Guardrails          => Set<MlflowGuardrail>();
    public DbSet<MlflowGuardrailConfig>    GuardrailConfigs    => Set<MlflowGuardrailConfig>();

    // ── Infrastructure ─────────────────────────────────────────────────────────
    public DbSet<MlflowEntityAssociation>    EntityAssociations   => Set<MlflowEntityAssociation>();
    public DbSet<MlflowWebhook>              Webhooks             => Set<MlflowWebhook>();
    public DbSet<MlflowWebhookEvent>         WebhookEvents        => Set<MlflowWebhookEvent>();
    public DbSet<MlflowJob>                  Jobs                 => Set<MlflowJob>();
    public DbSet<MlflowSecret>               Secrets              => Set<MlflowSecret>();
    public DbSet<MlflowWorkspace>            Workspaces           => Set<MlflowWorkspace>();
    public DbSet<MlflowBudgetPolicy>         BudgetPolicies       => Set<MlflowBudgetPolicy>();
    public DbSet<MlflowIssue>                Issues               => Set<MlflowIssue>();
    public DbSet<MlflowEndpoint>             Endpoints            => Set<MlflowEndpoint>();
    public DbSet<MlflowModelDefinition>      ModelDefinitions     => Set<MlflowModelDefinition>();
    public DbSet<MlflowEndpointModelMapping> EndpointModelMappings => Set<MlflowEndpointModelMapping>();
    public DbSet<MlflowEndpointBinding>      EndpointBindings     => Set<MlflowEndpointBinding>();
    public DbSet<MlflowEndpointTag>          EndpointTags         => Set<MlflowEndpointTag>();

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

        modelBuilder.Entity<MlflowDataset>()
            .HasKey(e => new { e.ExperimentId, e.Name, e.Digest });

        modelBuilder.Entity<MlflowInput>()
            .HasKey(e => new { e.SourceType, e.SourceId, e.DestinationType, e.DestinationId });

        modelBuilder.Entity<MlflowInputTag>()
            .HasKey(e => new { e.InputUuid, e.Name });

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

        modelBuilder.Entity<MlflowLoggedModelMetric>()
            .HasKey(e => new { e.ModelId, e.MetricName, e.MetricTimestampMs, e.MetricStep, e.RunId });

        modelBuilder.Entity<MlflowLoggedModelParam>()
            .HasKey(e => new { e.ModelId, e.ParamKey });

        modelBuilder.Entity<MlflowLoggedModelTag>()
            .HasKey(e => new { e.ModelId, e.TagKey });

        // ── Tracing ────────────────────────────────────────────────────────────
        modelBuilder.Entity<MlflowTraceTag>()
            .HasKey(e => new { e.Key, e.RequestId });

        modelBuilder.Entity<MlflowTraceRequestMetadata>()
            .HasKey(e => new { e.Key, e.RequestId });

        modelBuilder.Entity<MlflowTraceMetric>()
            .HasKey(e => new { e.RequestId, e.Key });

        modelBuilder.Entity<MlflowSpan>()
            .HasKey(e => new { e.TraceId, e.SpanId });

        modelBuilder.Entity<MlflowSpanMetric>()
            .HasKey(e => new { e.TraceId, e.SpanId, e.Key });

        // ── Evaluation & review ────────────────────────────────────────────────
        modelBuilder.Entity<MlflowEvaluationDatasetTag>()
            .HasKey(e => new { e.DatasetId, e.Key });

        modelBuilder.Entity<MlflowReviewQueueUser>()
            .HasKey(e => new { e.QueueId, e.UserId });

        modelBuilder.Entity<MlflowReviewQueueItem>()
            .HasKey(e => new { e.QueueId, e.ItemId });

        modelBuilder.Entity<MlflowReviewQueueLabelSchema>()
            .HasKey(e => new { e.QueueId, e.SchemaId });

        // ── Scoring & guardrails ───────────────────────────────────────────────
        modelBuilder.Entity<MlflowScorerVersion>()
            .HasKey(e => new { e.ScorerId, e.ScorerVersion });

        modelBuilder.Entity<MlflowGuardrailConfig>()
            .HasKey(e => new { e.EndpointId, e.GuardrailId });

        // ── Infrastructure ─────────────────────────────────────────────────────
        modelBuilder.Entity<MlflowEntityAssociation>()
            .HasKey(e => new { e.SourceType, e.SourceId, e.DestinationType, e.DestinationId });

        modelBuilder.Entity<MlflowWebhookEvent>()
            .HasKey(e => new { e.WebhookId, e.Entity, e.Action });

        modelBuilder.Entity<MlflowEndpointBinding>()
            .HasKey(e => new { e.EndpointId, e.ResourceType, e.ResourceId });

        modelBuilder.Entity<MlflowEndpointTag>()
            .HasKey(e => new { e.Key, e.EndpointId });
    }
}
