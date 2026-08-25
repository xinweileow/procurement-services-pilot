namespace Procurement.Api.Models.Dtos;

public sealed record ScoreEvaluationRequest(
    Guid SupplierId,
    string EvaluatorId,
    decimal TechnicalScore,
    decimal CommercialScore,
    string? Comments = null,
    bool Lock = false);

public sealed record EvaluationResponse(
    Guid Id,
    Guid RfxEventId,
    Guid SupplierId,
    string EvaluatorId,
    decimal TechnicalScore,
    decimal CommercialScore,
    string? Comments,
    string Status,
    DateTime? LockedAtUtc);

public sealed record RaiseClarificationRequest(
    Guid SupplierId,
    string Category,
    string Question,
    bool IsMaterialDeviation = false);

public sealed record ClarificationResponse(
    Guid Id,
    Guid RfxEventId,
    Guid SupplierId,
    string Category,
    string Question,
    string? Response,
    bool IsMaterialDeviation,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);

public sealed record ResolveClarificationRequest(
    string Response);

public sealed record StatusGateDecisionRequest(
    string Decision); // proceed | return | reject

public sealed record StatusGateDecisionResponse(
    Guid RfxEventId,
    string Decision,
    bool Passed,
    string Message);
