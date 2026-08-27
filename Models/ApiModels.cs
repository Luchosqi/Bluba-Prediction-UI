using System.Text.Json.Serialization;

namespace Bluba.Prediction.UI.Models;

// Los nombres siguen el contrato de openapi.yaml (snake_case en el JSON).

public sealed record CaseSummary(
    [property: JsonPropertyName("id_caso")] string IdCaso,
    [property: JsonPropertyName("rango_edad")] string RangoEdad,
    [property: JsonPropertyName("diagnostico_principal")] string DiagnosticoPrincipal,
    [property: JsonPropertyName("perfil_sensorial_predominante")] string PerfilSensorialPredominante,
    [property: JsonPropertyName("data_origin")] string DataOrigin);

public sealed record ProbabilityInterval(
    [property: JsonPropertyName("lower")] double Lower,
    [property: JsonPropertyName("upper")] double Upper,
    [property: JsonPropertyName("level")] double Level);

public sealed record HealthStatus(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("database")] string Database,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("contract_version")] string ContractVersion,
    [property: JsonPropertyName("timezone")] string Timezone);

public sealed record PredictRequest(
    [property: JsonPropertyName("cutoff")] DateOnly Cutoff);

public sealed record RiskPrediction(
    [property: JsonPropertyName("prediction_id")] int PredictionId,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("cutoff")] DateOnly Cutoff,
    [property: JsonPropertyName("risk_probability")] double RiskProbability,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("risk_level")] string RiskLevel,
    [property: JsonPropertyName("wallet_score")] int WalletScore,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("training_source")] string TrainingSource,
    [property: JsonPropertyName("probability_interval")] ProbabilityInterval? ProbabilityInterval = null,
    [property: JsonPropertyName("decision_threshold")] double? DecisionThreshold = null,
    [property: JsonPropertyName("alert_triggered")] bool? AlertTriggered = null);

public sealed record PredictionSnapshot(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("risk_probability")] double RiskProbability,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("model_version")] string ModelVersion);

public sealed record RiskChangeItem(
    [property: JsonPropertyName("feature")] string Feature,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("previous_value")] string? PreviousValue,
    [property: JsonPropertyName("current_value")] string? CurrentValue,
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("relative_change_contribution")] double RelativeChangeContribution);

public sealed record RiskChangeExplanation(
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("comparison_supported")] bool ComparisonSupported,
    [property: JsonPropertyName("current_prediction")] PredictionSnapshot CurrentPrediction,
    [property: JsonPropertyName("risk_change")] RiskChange RiskChange,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("previous_prediction")] PredictionSnapshot? PreviousPrediction = null,
    [property: JsonPropertyName("main_changes")] List<RiskChangeItem>? MainChanges = null);

public sealed record RiskChange(
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("delta_probability_points")] double DeltaProbabilityPoints);

public sealed record StrategyRecommendation(
    [property: JsonPropertyName("strategy")] string Strategy,
    [property: JsonPropertyName("previous_outcome")] string PreviousOutcome,
    [property: JsonPropertyName("matched_signals")] List<string> MatchedSignals);

public sealed record Strategies(
    [property: JsonPropertyName("similar_previous_events")] int SimilarPreviousEvents,
    [property: JsonPropertyName("strategies")] List<StrategyRecommendation> Items);

public sealed record TriggerSummary(
    [property: JsonPropertyName("raw_text")] string? RawText,
    // El contrato marca tags como opcional (default_factory=list), así que puede no venir.
    [property: JsonPropertyName("tags")] List<string>? Tags);

public sealed record DysregulationStrategy(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("result")] string Result);

public sealed record DysregulationHistoryItem(
    [property: JsonPropertyName("event_id")] string EventId,
    [property: JsonPropertyName("occurred_at")] DateTime OccurredAt,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("intensity")] string Intensity,
    [property: JsonPropertyName("severity_level")] int? SeverityLevel,
    [property: JsonPropertyName("suspected_trigger")] TriggerSummary SuspectedTrigger,
    [property: JsonPropertyName("strategy")] DysregulationStrategy Strategy);

public sealed record DysregulationHistory(
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("items")] List<DysregulationHistoryItem> Items);

public sealed record DysregulationCreate(
    [property: JsonPropertyName("occurred_at")] DateTime OccurredAt,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("intensity")] string Intensity,
    [property: JsonPropertyName("strategy_applied")] string StrategyApplied,
    [property: JsonPropertyName("strategy_result")] string StrategyResult,
    [property: JsonPropertyName("reported_by_type")] string ReportedByType,
    [property: JsonPropertyName("client_event_id")] string ClientEventId,
    [property: JsonPropertyName("suspected_trigger_text")] string? SuspectedTriggerText = null);

public sealed record DysregulationEventApi(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("occurred_at")] DateTime OccurredAt,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("intensity")] string Intensity,
    [property: JsonPropertyName("severity_level")] int? SeverityLevel,
    [property: JsonPropertyName("client_event_id")] string? ClientEventId = null);

public sealed record DysregulationCreated(
    [property: JsonPropertyName("event")] DysregulationEventApi Event);

public sealed record InterventionCreate(
    [property: JsonPropertyName("occurred_at")] DateTime OccurredAt,
    [property: JsonPropertyName("intervention_type")] string InterventionType,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("adherence")] string Adherence,
    [property: JsonPropertyName("client_intervention_id")] string ClientInterventionId,
    [property: JsonPropertyName("prediction_id")] int? PredictionId = null);

public sealed record InterventionOutcomeUpdate(
    [property: JsonPropertyName("outcome_observed_at")] DateTime OutcomeObservedAt,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("notes")] string? Notes = null);

public sealed record Intervention(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("occurred_at")] DateTime OccurredAt,
    [property: JsonPropertyName("recorded_at")] DateTime RecordedAt,
    [property: JsonPropertyName("intervention_type")] string InterventionType,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("adherence")] string Adherence,
    [property: JsonPropertyName("client_intervention_id")] string ClientInterventionId,
    [property: JsonPropertyName("prediction_id")] int? PredictionId = null,
    [property: JsonPropertyName("outcome_observed_at")] DateTime? OutcomeObservedAt = null,
    [property: JsonPropertyName("outcome")] string? Outcome = null,
    [property: JsonPropertyName("outcome_notes")] string? OutcomeNotes = null);

public sealed record AdaptiveQuestion(
    [property: JsonPropertyName("needs_more_information")] bool NeedsMoreInformation,
    [property: JsonPropertyName("importance")] double Importance,
    [property: JsonPropertyName("feature")] string? Feature = null,
    [property: JsonPropertyName("question")] string? Question = null,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("question_id")] string? QuestionId = null,
    [property: JsonPropertyName("information_value")] double? InformationValue = null,
    [property: JsonPropertyName("possible_risk_range")] Dictionary<string, double>? PossibleRiskRange = null,
    [property: JsonPropertyName("prediction_range")] double? PredictionRange = null,
    [property: JsonPropertyName("options")] List<AdaptiveOption>? Options = null);

public sealed record AdaptiveOption(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("label")] string Label);

public sealed record AdaptiveAnswerCreate(
    [property: JsonPropertyName("question_id")] string QuestionId,
    [property: JsonPropertyName("feature_name")] string FeatureName,
    [property: JsonPropertyName("raw_answer")] string RawAnswer,
    [property: JsonPropertyName("observed_at")] DateTime ObservedAt,
    [property: JsonPropertyName("client_response_id")] string ClientResponseId,
    [property: JsonPropertyName("source")] string Source = "ADAPTIVE_QUESTION");

public sealed record AdaptiveAnswer(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("question_id")] string QuestionId,
    [property: JsonPropertyName("feature_name")] string FeatureName,
    [property: JsonPropertyName("raw_answer")] string RawAnswer,
    [property: JsonPropertyName("normalized_value")] string NormalizedValue,
    [property: JsonPropertyName("observed_at")] DateTime ObservedAt,
    [property: JsonPropertyName("recorded_at")] DateTime RecordedAt,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("client_response_id")] string ClientResponseId);

public sealed record AdaptiveAnswerResult(
    [property: JsonPropertyName("response")] AdaptiveAnswer Response,
    [property: JsonPropertyName("prediction_before")] RiskPrediction PredictionBefore,
    [property: JsonPropertyName("prediction_after")] RiskPrediction PredictionAfter);
