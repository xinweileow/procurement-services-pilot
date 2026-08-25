namespace Procurement.Api.Models.Dtos;

public sealed record RouteRecommendationRequest(
    Guid? RequestId,
    decimal EstimatedValue,
    string Currency = "MYR",
    string Country = "Malaysia",
    bool SingleSource = false,
    bool Emergency = false);

public sealed record RouteRecommendationResponse(
    string RecommendedRoute,
    int MinQuotations,
    bool RequiresTender,
    bool RequiresGspReroute);

public sealed record ThreePointCheckResponse(
    Guid Id,
    Guid SupplierId,
    bool IdentityVerified,
    bool RegistrationEvidenceVerified,
    bool ActiveStatus,
    DateTime CheckedAtUtc);

public sealed record AgreeSourcingStrategyRequest(
    bool RequesterConfirmation = true);

public sealed record SourcingStrategyResponse(
    Guid Id,
    Guid RequestId,
    string RecommendedRoute,
    string? MarketApproach,
    string? EvaluationMethod,
    string Status,
    DateTime CreatedAtUtc)
{
    public static SourcingStrategyResponse From(SourcingStrategy strategy) =>
        new(strategy.Id, strategy.RequestId, strategy.RecommendedRoute, strategy.MarketApproach,
            strategy.EvaluationMethod, strategy.Status, strategy.CreatedAtUtc);
}

public sealed record SpendAnalysisResponse(
    decimal HistoricalSpend,
    decimal BenchmarkPrice,
    decimal PriceVariancePct,
    bool TailSpendFlag);

public sealed record TriageDecisionRequest(
    string Decision,
    string? Reason = null,
    string? AssigneeTeam = null);

public sealed record TriageDecisionResponse(
    Guid Id,
    string Status,
    TriageDecisionItem TriageDecision);

public sealed record TriageDecisionItem(
    Guid Id,
    Guid RequestId,
    string Decision,
    string? Reason,
    string? AssigneeTeam,
    string DecidedBy,
    DateTime DecidedAtUtc);

