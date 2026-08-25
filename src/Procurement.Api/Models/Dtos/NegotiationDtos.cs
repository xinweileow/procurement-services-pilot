namespace Procurement.Api.Models.Dtos;

public sealed record RecordNegotiationRoundRequest(
    Guid SupplierId,
    decimal RevisedPrice,
    string? Terms,
    string RoundType); // negotiation | eAuction

public sealed record NegotiationRoundResponse(
    Guid Id,
    int RoundNumber);

public sealed record RequestBafoRequest(
    Guid SupplierId,
    DateTime Deadline);

public sealed record BafoRequestResponse(
    Guid Id,
    string Status);

public sealed record ConsolidatedResultItem(
    Guid SupplierId,
    decimal TotalScore,
    string RecommendedScenario);

public sealed record SavingsCalculationRequest(
    decimal? BaselineValue,
    decimal AwardedValue,
    string Method); // hard | soft

public sealed record SavingsCalculationResponse(
    decimal SavingsAmount,
    string SavingsType,
    bool FinanceValidated);
