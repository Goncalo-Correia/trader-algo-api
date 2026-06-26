using System.Text.Json.Serialization;

namespace TraderAlgoApi.Dtos.Ml;

public sealed record MlflowMetricPointDto(
    [property: JsonPropertyName("step")]      long Step,
    [property: JsonPropertyName("value")]     double? Value,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);

public sealed record MlflowTrainingTrackingSummaryDto(
    [property: JsonPropertyName("trackingAvailable")] bool TrackingAvailable,
    [property: JsonPropertyName("mlflowRunUuid")]     string? MlflowRunUuid,
    [property: JsonPropertyName("runName")]           string? RunName,
    [property: JsonPropertyName("status")]            string? Status,
    [property: JsonPropertyName("startTime")]         DateTimeOffset? StartTime,
    [property: JsonPropertyName("endTime")]           DateTimeOffset? EndTime,
    [property: JsonPropertyName("finalBalance")]      double? FinalBalance,
    [property: JsonPropertyName("pnlPct")]            double? PnlPct,
    [property: JsonPropertyName("nTrades")]           int? NTrades,
    [property: JsonPropertyName("params")]            IReadOnlyDictionary<string, string> Params,
    [property: JsonPropertyName("message")]           string? Message = null)
{
    public static MlflowTrainingTrackingSummaryDto Unavailable(
        string? message = null) =>
        new(
            TrackingAvailable: false,
            MlflowRunUuid: null,
            RunName: null,
            Status: null,
            StartTime: null,
            EndTime: null,
            FinalBalance: null,
            PnlPct: null,
            NTrades: null,
            Params: new Dictionary<string, string>(),
            Message: message);
}

public sealed record MlflowTrackedMetricDto(
    [property: JsonPropertyName("key")]          string? Key,
    [property: JsonPropertyName("label")]        string Label,
    [property: JsonPropertyName("whatItChecks")] string WhatItChecks,
    [property: JsonPropertyName("latestValue")]  double? LatestValue,
    [property: JsonPropertyName("history")]      IReadOnlyList<MlflowMetricPointDto> History);

public sealed record MlflowRewardPerformanceMetricsDto(
    [property: JsonPropertyName("averageEpisodeReturn")] MlflowTrackedMetricDto AverageEpisodeReturn,
    [property: JsonPropertyName("medianEpisodeReturn")]  MlflowTrackedMetricDto MedianEpisodeReturn,
    [property: JsonPropertyName("cumulativeReward")]     MlflowTrackedMetricDto CumulativeReward,
    [property: JsonPropertyName("rewardPerStep")]        MlflowTrackedMetricDto RewardPerStep,
    [property: JsonPropertyName("successRate")]          MlflowTrackedMetricDto SuccessRate,
    [property: JsonPropertyName("episodeLength")]        MlflowTrackedMetricDto EpisodeLength);

public sealed record MlflowRewardStabilityMetricsDto(
    [property: JsonPropertyName("rewardVariance")] MlflowTrackedMetricDto RewardVariance,
    [property: JsonPropertyName("worstCaseReturn")] MlflowTrackedMetricDto WorstCaseReturn,
    [property: JsonPropertyName("tailRisk")] MlflowTrackedMetricDto TailRisk,
    [property: JsonPropertyName("failureRate")] MlflowTrackedMetricDto FailureRate);

public sealed record MlflowRewardLearningQualityMetricsDto(
    [property: JsonPropertyName("regret")]                         MlflowTrackedMetricDto Regret,
    [property: JsonPropertyName("policyImprovementOverBaseline")]  MlflowTrackedMetricDto PolicyImprovementOverBaseline,
    [property: JsonPropertyName("explorationRate")]                MlflowTrackedMetricDto ExplorationRate,
    [property: JsonPropertyName("entropy")]                        MlflowTrackedMetricDto Entropy,
    [property: JsonPropertyName("offPolicyEvaluationScore")]       MlflowTrackedMetricDto OffPolicyEvaluationScore,
    [property: JsonPropertyName("valueLoss")]                      MlflowTrackedMetricDto ValueLoss,
    [property: JsonPropertyName("policyLoss")]                     MlflowTrackedMetricDto PolicyLoss,
    [property: JsonPropertyName("klDivergence")]                   MlflowTrackedMetricDto KlDivergence);

public sealed record MlflowRewardSafetyMetricsDto(
    [property: JsonPropertyName("constraintViolationRate")] MlflowTrackedMetricDto ConstraintViolationRate,
    [property: JsonPropertyName("unsafeActionRate")]       MlflowTrackedMetricDto UnsafeActionRate,
    [property: JsonPropertyName("safetyRefusalRate")]      MlflowTrackedMetricDto SafetyRefusalRate,
    [property: JsonPropertyName("safetyViolationRate")]    MlflowTrackedMetricDto SafetyViolationRate);

public sealed record MlflowRewardBaselineComparisonMetricsDto(
    [property: JsonPropertyName("randomPolicy")]              MlflowTrackedMetricDto RandomPolicy,
    [property: JsonPropertyName("previousProductionModel")]   MlflowTrackedMetricDto PreviousProductionModel,
    [property: JsonPropertyName("ruleBasedHeuristic")]        MlflowTrackedMetricDto RuleBasedHeuristic,
    [property: JsonPropertyName("humanExpert")]               MlflowTrackedMetricDto HumanExpert,
    [property: JsonPropertyName("oracleOrSimulatorOptimum")]  MlflowTrackedMetricDto OracleOrSimulatorOptimum,
    [property: JsonPropertyName("upliftOverBaseline")]        MlflowTrackedMetricDto UpliftOverBaseline);

public sealed record MlflowRewardRobustnessMetricsDto(
    [property: JsonPropertyName("generalizationAcrossEnvironments")] MlflowTrackedMetricDto GeneralizationAcrossEnvironments,
    [property: JsonPropertyName("performanceAcrossScenarios")]       MlflowTrackedMetricDto PerformanceAcrossScenarios,
    [property: JsonPropertyName("coverage")]                         MlflowTrackedMetricDto Coverage,
    [property: JsonPropertyName("diversity")]                        MlflowTrackedMetricDto Diversity,
    [property: JsonPropertyName("delayedRewardAttribution")]         MlflowTrackedMetricDto DelayedRewardAttribution);

public sealed record MlflowRewardIntegrityMetricsDto(
    [property: JsonPropertyName("rewardHackingIndicators")]       MlflowTrackedMetricDto RewardHackingIndicators,
    [property: JsonPropertyName("rewardSuccessGap")]             MlflowTrackedMetricDto RewardSuccessGap,
    [property: JsonPropertyName("trainEvalRewardGap")]           MlflowTrackedMetricDto TrainEvalRewardGap,
    [property: JsonPropertyName("proxyRewardOveroptimization")]  MlflowTrackedMetricDto ProxyRewardOveroptimization,
    [property: JsonPropertyName("humanPreferenceWinRate")]       MlflowTrackedMetricDto HumanPreferenceWinRate,
    [property: JsonPropertyName("humanManualAuditScore")]        MlflowTrackedMetricDto HumanManualAuditScore);

public sealed record MlflowRewardTrackingDashboardDto(
    [property: JsonPropertyName("performance")]        MlflowRewardPerformanceMetricsDto Performance,
    [property: JsonPropertyName("stability")]          MlflowRewardStabilityMetricsDto Stability,
    [property: JsonPropertyName("learningQuality")]    MlflowRewardLearningQualityMetricsDto LearningQuality,
    [property: JsonPropertyName("safety")]             MlflowRewardSafetyMetricsDto Safety,
    [property: JsonPropertyName("baselineComparison")] MlflowRewardBaselineComparisonMetricsDto BaselineComparison,
    [property: JsonPropertyName("robustness")]         MlflowRewardRobustnessMetricsDto Robustness,
    [property: JsonPropertyName("rewardIntegrity")]    MlflowRewardIntegrityMetricsDto RewardIntegrity)
{
    public static MlflowRewardTrackingDashboardDto From(
        IReadOnlyDictionary<string, double?> latestMetrics,
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory) =>
        new(
            Performance: new MlflowRewardPerformanceMetricsDto(
                AverageEpisodeReturn: Metric(
                    latestMetrics,
                    metricHistory,
                    "Average episode return",
                    "Whether the policy is earning more reward over time.",
                    "average_episode_return",
                    "avg_episode_return",
                    "mean_episode_return",
                    "episode_return_mean",
                    "rollout/ep_rew_mean"),
                MedianEpisodeReturn: Metric(
                    latestMetrics,
                    metricHistory,
                    "Median episode return",
                    "The typical episode return, less distorted by outlier runs than the mean.",
                    "median_episode_return",
                    "episode_return_median",
                    "reward_median",
                    "return_median"),
                CumulativeReward: Metric(
                    latestMetrics,
                    metricHistory,
                    "Cumulative reward",
                    "The total reward accumulated by the policy during training or evaluation.",
                    "cumulative_reward",
                    "total_reward",
                    "episode_reward",
                    "episode_return",
                    "reward"),
                RewardPerStep: Metric(
                    latestMetrics,
                    metricHistory,
                    "Reward per step",
                    "Reward efficiency when episodes have different lengths.",
                    "reward_per_step",
                    "avg_reward_per_step",
                    "mean_reward_per_step",
                    "step_reward_mean"),
                SuccessRate: Metric(
                    latestMetrics,
                    metricHistory,
                    "Success rate",
                    "Whether the agent achieves the intended outcome, not just high reward.",
                    "success_rate",
                    "episode_success_rate",
                    "eval/success_rate",
                    "goal_success_rate"),
                EpisodeLength: Metric(
                    latestMetrics,
                    metricHistory,
                    "Episode length",
                    "Whether the agent finishes tasks faster or simply prolongs reward collection.",
                    "episode_length",
                    "avg_episode_length",
                    "mean_episode_length",
                    "rollout/ep_len_mean")),
            Stability: new MlflowRewardStabilityMetricsDto(
                RewardVariance: Metric(
                    latestMetrics,
                    metricHistory,
                    "Reward variance",
                    "Whether reward performance is stable or highly inconsistent.",
                    "reward_variance",
                    "episode_return_variance",
                    "return_variance",
                    "rollout/ep_rew_var"),
                WorstCaseReturn: Metric(
                    latestMetrics,
                    metricHistory,
                    "Worst-case return",
                    "Whether the model occasionally fails badly despite good average performance.",
                    "worst_case_return",
                    "min_episode_return",
                    "minimum_episode_return",
                    "return_min"),
                TailRisk: Metric(
                    latestMetrics,
                    metricHistory,
                    "Tail risk",
                    "Low-percentile reward outcomes that expose rare but severe failures.",
                    "tail_risk",
                    "return_p05",
                    "return_p01",
                    "value_at_risk",
                    "var_95",
                    "cvar_95"),
                FailureRate: Metric(
                    latestMetrics,
                    metricHistory,
                    "Failure rate",
                    "How often the model misses the objective or terminates unsuccessfully.",
                    "failure_rate",
                    "episode_failure_rate",
                    "eval/failure_rate")),
            LearningQuality: new MlflowRewardLearningQualityMetricsDto(
                Regret: Metric(
                    latestMetrics,
                    metricHistory,
                    "Regret",
                    "How much reward is lost compared with the best known policy or action.",
                    "regret",
                    "cumulative_regret",
                    "average_regret",
                    "instant_regret"),
                PolicyImprovementOverBaseline: Metric(
                    latestMetrics,
                    metricHistory,
                    "Policy improvement over baseline",
                    "Whether the new model beats a heuristic, previous model, or human baseline.",
                    "policy_improvement_over_baseline",
                    "baseline_improvement",
                    "improvement_over_baseline",
                    "uplift_over_baseline",
                    "uplift"),
                ExplorationRate: Metric(
                    latestMetrics,
                    metricHistory,
                    "Exploration rate",
                    "Whether the policy is still exploring or has collapsed too early.",
                    "exploration_rate",
                    "epsilon",
                    "random_action_rate"),
                Entropy: Metric(
                    latestMetrics,
                    metricHistory,
                    "Entropy",
                    "The spread of policy actions, useful for detecting exploration collapse.",
                    "entropy",
                    "policy_entropy",
                    "action_entropy",
                    "train/entropy_loss"),
                OffPolicyEvaluationScore: Metric(
                    latestMetrics,
                    metricHistory,
                    "Off-policy evaluation score",
                    "An estimate of policy performance before deploying it online.",
                    "off_policy_evaluation_score",
                    "ope_score",
                    "ips_score",
                    "doubly_robust_score",
                    "dr_score"),
                ValueLoss: Metric(
                    latestMetrics,
                    metricHistory,
                    "Value loss",
                    "Training stability of the value function in reinforcement learning.",
                    "value_loss",
                    "train/value_loss"),
                PolicyLoss: Metric(
                    latestMetrics,
                    metricHistory,
                    "Policy loss",
                    "Training stability of the policy optimizer.",
                    "policy_loss",
                    "train/policy_loss",
                    "train/policy_gradient_loss"),
                KlDivergence: Metric(
                    latestMetrics,
                    metricHistory,
                    "KL divergence",
                    "How far the policy has moved from the reference or previous policy.",
                    "kl_divergence",
                    "approx_kl",
                    "train/approx_kl",
                    "policy_kl")),
            Safety: new MlflowRewardSafetyMetricsDto(
                ConstraintViolationRate: Metric(
                    latestMetrics,
                    metricHistory,
                    "Constraint violation rate",
                    "Whether the model gets reward while breaking business, risk, or safety rules.",
                    "constraint_violation_rate",
                    "violation_rate",
                    "rule_violation_rate"),
                UnsafeActionRate: Metric(
                    latestMetrics,
                    metricHistory,
                    "Unsafe action rate",
                    "How often the policy chooses actions that should not be deployed.",
                    "unsafe_action_rate",
                    "unsafe_rate",
                    "unsafe_actions_rate"),
                SafetyRefusalRate: Metric(
                    latestMetrics,
                    metricHistory,
                    "Safety refusal rate",
                    "For RLHF-style systems, whether the policy refuses unsafe requests appropriately.",
                    "safety_refusal_rate",
                    "refusal_rate",
                    "safe_refusal_rate"),
                SafetyViolationRate: Metric(
                    latestMetrics,
                    metricHistory,
                    "Safety violation rate",
                    "For RLHF-style systems, whether reward optimization harms safety behavior.",
                    "safety_violation_rate",
                    "unsafe_completion_rate",
                    "policy_violation_rate")),
            BaselineComparison: new MlflowRewardBaselineComparisonMetricsDto(
                RandomPolicy: Metric(
                    latestMetrics,
                    metricHistory,
                    "Random policy baseline",
                    "The policy's reward or success compared with random action selection.",
                    "random_policy_return",
                    "random_policy_reward",
                    "random_baseline_return",
                    "random_baseline_reward"),
                PreviousProductionModel: Metric(
                    latestMetrics,
                    metricHistory,
                    "Previous production model baseline",
                    "The policy's reward or success compared with the prior deployed model.",
                    "previous_model_return",
                    "previous_model_reward",
                    "production_baseline_return",
                    "production_baseline_reward"),
                RuleBasedHeuristic: Metric(
                    latestMetrics,
                    metricHistory,
                    "Rule-based heuristic baseline",
                    "The policy's reward or success compared with a simple rule-based strategy.",
                    "heuristic_return",
                    "heuristic_reward",
                    "rule_based_return",
                    "rule_based_reward"),
                HumanExpert: Metric(
                    latestMetrics,
                    metricHistory,
                    "Human expert baseline",
                    "The policy's reward or preference score compared with a human expert.",
                    "human_expert_return",
                    "human_expert_reward",
                    "human_baseline_score"),
                OracleOrSimulatorOptimum: Metric(
                    latestMetrics,
                    metricHistory,
                    "Oracle or simulator optimum",
                    "The best known achievable reward in simulation or hindsight.",
                    "oracle_return",
                    "oracle_reward",
                    "simulator_optimum_return",
                    "optimal_return"),
                UpliftOverBaseline: Metric(
                    latestMetrics,
                    metricHistory,
                    "Uplift over baseline",
                    "The practical value gained over the selected baseline.",
                    "uplift",
                    "uplift_over_baseline",
                    "baseline_uplift",
                    "conversion_uplift")),
            Robustness: new MlflowRewardRobustnessMetricsDto(
                GeneralizationAcrossEnvironments: Metric(
                    latestMetrics,
                    metricHistory,
                    "Generalization across environments",
                    "Whether learned behavior holds up outside the exact training setup.",
                    "generalization_score",
                    "eval_generalization_score",
                    "cross_env_success_rate",
                    "cross_environment_return"),
                PerformanceAcrossScenarios: Metric(
                    latestMetrics,
                    metricHistory,
                    "Performance across scenarios",
                    "Reward or success across important slices, scenarios, or market regimes.",
                    "scenario_performance",
                    "slice_performance",
                    "scenario_return",
                    "slice_success_rate"),
                Coverage: Metric(
                    latestMetrics,
                    metricHistory,
                    "Coverage",
                    "Whether a bandit or recommender avoids repeatedly choosing only a few actions.",
                    "coverage",
                    "action_coverage",
                    "item_coverage"),
                Diversity: Metric(
                    latestMetrics,
                    metricHistory,
                    "Diversity",
                    "Whether recommendations or selected actions remain sufficiently varied.",
                    "diversity",
                    "action_diversity",
                    "recommendation_diversity"),
                DelayedRewardAttribution: Metric(
                    latestMetrics,
                    metricHistory,
                    "Delayed reward attribution",
                    "Whether delayed outcomes are assigned to the right action or decision.",
                    "delayed_reward_attribution",
                    "attribution_accuracy",
                    "delayed_attribution_score")),
            RewardIntegrity: new MlflowRewardIntegrityMetricsDto(
                RewardHackingIndicators: Metric(
                    latestMetrics,
                    metricHistory,
                    "Reward hacking indicators",
                    "Whether the model exploits loopholes in the reward function.",
                    "reward_hacking_score",
                    "reward_hacking_indicator",
                    "reward_hacking_rate"),
                RewardSuccessGap: Metric(
                    latestMetrics,
                    metricHistory,
                    "Reward-success gap",
                    "Cases where reward improves while the real-world objective does not.",
                    "reward_success_gap",
                    "reward_objective_gap",
                    "reward_outcome_gap"),
                TrainEvalRewardGap: Metric(
                    latestMetrics,
                    metricHistory,
                    "Train-evaluation reward gap",
                    "Whether the policy performs well in training but poorly in new environments.",
                    "train_eval_reward_gap",
                    "train_eval_gap",
                    "train_test_reward_gap"),
                ProxyRewardOveroptimization: Metric(
                    latestMetrics,
                    metricHistory,
                    "Proxy reward overoptimization",
                    "Whether the policy maximizes easy proxy signals instead of the real objective.",
                    "proxy_reward_overoptimization",
                    "proxy_reward_gap",
                    "overoptimization_score"),
                HumanPreferenceWinRate: Metric(
                    latestMetrics,
                    metricHistory,
                    "Human preference win rate",
                    "For RLHF or reward-model systems, whether humans prefer the trained policy.",
                    "human_preference_win_rate",
                    "human_eval_win_rate",
                    "preference_win_rate",
                    "win_rate"),
                HumanManualAuditScore: Metric(
                    latestMetrics,
                    metricHistory,
                    "Human/manual audit score",
                    "Independent outcome checks that verify reward against the real objective.",
                    "human_manual_audit_score",
                    "manual_audit_score",
                    "human_eval_score")));

    private static MlflowTrackedMetricDto Metric(
        IReadOnlyDictionary<string, double?> latestMetrics,
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory,
        string label,
        string whatItChecks,
        params string[] candidateKeys)
    {
        var key = FindMetricKey(latestMetrics, metricHistory, candidateKeys);
        var history = key is not null
            ? FindMetricHistory(metricHistory, key)
            : [];
        var latestValue = key is not null && TryGetLatestMetricValue(latestMetrics, key, out var value)
            ? value
            : history.LastOrDefault()?.Value;

        return new MlflowTrackedMetricDto(
            Key: key,
            Label: label,
            WhatItChecks: whatItChecks,
            LatestValue: latestValue,
            History: history);
    }

    private static bool TryGetLatestMetricValue(
        IReadOnlyDictionary<string, double?> latestMetrics,
        string metricKey,
        out double? value)
    {
        var key = latestMetrics.Keys.FirstOrDefault(
            candidate => candidate.Equals(metricKey, StringComparison.OrdinalIgnoreCase));
        if (key is not null)
            return latestMetrics.TryGetValue(key, out value);

        value = null;
        return false;
    }

    private static IReadOnlyList<MlflowMetricPointDto> FindMetricHistory(
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory,
        string metricKey)
    {
        var key = metricHistory.Keys.FirstOrDefault(
            candidate => candidate.Equals(metricKey, StringComparison.OrdinalIgnoreCase));

        return key is not null && metricHistory.TryGetValue(key, out var points)
            ? points
            : [];
    }

    private static string? FindMetricKey(
        IReadOnlyDictionary<string, double?> latestMetrics,
        IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> metricHistory,
        params string[] candidateKeys)
    {
        foreach (var candidateKey in candidateKeys)
        {
            var latestKey = latestMetrics.Keys.FirstOrDefault(
                key => key.Equals(candidateKey, StringComparison.OrdinalIgnoreCase));
            if (latestKey is not null)
                return latestKey;

            var historyKey = metricHistory.Keys.FirstOrDefault(
                key => key.Equals(candidateKey, StringComparison.OrdinalIgnoreCase));
            if (historyKey is not null)
                return historyKey;
        }

        return null;
    }
}

public sealed record MlflowExperimentInfoDto(
    [property: JsonPropertyName("experimentId")]    int ExperimentId,
    [property: JsonPropertyName("name")]            string Name,
    [property: JsonPropertyName("lifecycleStage")] string? LifecycleStage,
    [property: JsonPropertyName("creationTime")]   DateTimeOffset? CreationTime);

public sealed record MlflowRunTagsDto(
    [property: JsonPropertyName("user")]       string? User,
    [property: JsonPropertyName("sourceName")] string? SourceName,
    [property: JsonPropertyName("sourceType")] string? SourceType);

public sealed record MlflowModelVersionDto(
    [property: JsonPropertyName("version")]         int Version,
    [property: JsonPropertyName("currentStage")]    string? CurrentStage,
    [property: JsonPropertyName("source")]          string? Source,
    [property: JsonPropertyName("storageLocation")] string? StorageLocation,
    [property: JsonPropertyName("creationTime")]    DateTimeOffset? CreationTime,
    [property: JsonPropertyName("description")]     string? Description,
    [property: JsonPropertyName("runId")]           string? RunId);

public sealed record MlflowModelRegistryDto(
    [property: JsonPropertyName("modelName")]        string ModelName,
    [property: JsonPropertyName("modelDescription")] string? ModelDescription,
    [property: JsonPropertyName("registeredAt")]     DateTimeOffset? RegisteredAt,
    [property: JsonPropertyName("thisRunVersion")]   MlflowModelVersionDto? ThisRunVersion,
    [property: JsonPropertyName("allVersions")]      IReadOnlyList<MlflowModelVersionDto> AllVersions);

public sealed record MlflowPpoInternalsDto(
    [property: JsonPropertyName("policyGradientLoss")] MlflowTrackedMetricDto PolicyGradientLoss,
    [property: JsonPropertyName("valueLoss")]           MlflowTrackedMetricDto ValueLoss,
    [property: JsonPropertyName("entropyLoss")]         MlflowTrackedMetricDto EntropyLoss,
    [property: JsonPropertyName("approxKl")]            MlflowTrackedMetricDto ApproxKl,
    [property: JsonPropertyName("clipFraction")]        MlflowTrackedMetricDto ClipFraction,
    [property: JsonPropertyName("explainedVariance")]   MlflowTrackedMetricDto ExplainedVariance,
    [property: JsonPropertyName("epRewMean")]           MlflowTrackedMetricDto EpRewMean,
    [property: JsonPropertyName("epLenMean")]           MlflowTrackedMetricDto EpLenMean);

public sealed record MlflowTrainingTrackingResponse(
    [property: JsonPropertyName("trainingRunId")]      long TrainingRunId,
    [property: JsonPropertyName("trackingAvailable")] bool TrackingAvailable,
    [property: JsonPropertyName("mlflowRunUuid")]     string? MlflowRunUuid,
    [property: JsonPropertyName("runName")]           string? RunName,
    [property: JsonPropertyName("status")]            string? Status,
    [property: JsonPropertyName("startTime")]         DateTimeOffset? StartTime,
    [property: JsonPropertyName("endTime")]           DateTimeOffset? EndTime,
    [property: JsonPropertyName("artifactUri")]       string? ArtifactUri,
    [property: JsonPropertyName("params")]            IReadOnlyDictionary<string, string> Params,
    [property: JsonPropertyName("rewardMetrics")]     MlflowRewardTrackingDashboardDto RewardMetrics,
    [property: JsonPropertyName("latestMetrics")]     IReadOnlyDictionary<string, double?> LatestMetrics,
    [property: JsonPropertyName("metricHistory")]     IReadOnlyDictionary<string, IReadOnlyList<MlflowMetricPointDto>> MetricHistory,
    [property: JsonPropertyName("message")]           string? Message = null,
    [property: JsonPropertyName("experiment")]        MlflowExperimentInfoDto? Experiment = null,
    [property: JsonPropertyName("tags")]              MlflowRunTagsDto? Tags = null,
    [property: JsonPropertyName("registry")]          MlflowModelRegistryDto? Registry = null,
    [property: JsonPropertyName("ppoInternals")]      MlflowPpoInternalsDto? PpoInternals = null,
    [property: JsonPropertyName("evalMetrics")]       IReadOnlyDictionary<string, double?>? EvalMetrics = null)
{
    public static MlflowTrainingTrackingResponse Unavailable(
        long trainingRunId,
        string? message = null) =>
        new(
            TrainingRunId: trainingRunId,
            TrackingAvailable: false,
            MlflowRunUuid: null,
            RunName: null,
            Status: null,
            StartTime: null,
            EndTime: null,
            ArtifactUri: null,
            Params: new Dictionary<string, string>(),
            RewardMetrics: MlflowRewardTrackingDashboardDto.From(
                new Dictionary<string, double?>(),
                new Dictionary<string, IReadOnlyList<MlflowMetricPointDto>>()),
            LatestMetrics: new Dictionary<string, double?>(),
            MetricHistory: new Dictionary<string, IReadOnlyList<MlflowMetricPointDto>>(),
            Message: message);

    public MlflowTrainingTrackingSummaryDto ToSummary()
    {
        var finalBalance = LatestMetrics.TryGetValue("final_balance", out var rawFinalBalance)
            ? rawFinalBalance
            : null;
        var pnlPct = LatestMetrics.TryGetValue("pnl_pct", out var rawPnlPct)
            ? rawPnlPct
            : null;
        var nTrades = LatestMetrics.TryGetValue("n_trades", out var rawTrades) && rawTrades.HasValue
            ? (int?)Convert.ToInt32(rawTrades.Value)
            : null;

        return new MlflowTrainingTrackingSummaryDto(
            TrackingAvailable,
            MlflowRunUuid,
            RunName,
            Status,
            StartTime,
            EndTime,
            finalBalance,
            pnlPct,
            nTrades,
            Params,
            Message);
    }
}
