namespace Procurement.Api.Models.Dtos;

public sealed record CreateAwardRequest(
    Guid RfxEventId,
    string? ConsolidatedResultRef,
    Guid RecommendedSupplierId,
    string? Scenario);

public sealed record AwardResponse(
    Guid Id,
    string Status);

public sealed record FinalDueDiligenceRequest(
    string? ExemptionReason,
    string? ExemptionApproverId);

public sealed record FinalDueDiligenceResponse(
    Guid Id,
    bool Ready,
    bool ExemptionRecorded);

public sealed record CreateContractRequest(
    Guid AwardId,
    Guid? TemplateId,
    decimal Value = 0m);

public sealed record ContractResponse(
    Guid Id,
    string Status);

public sealed record SignContractRequest(
    string SignatoryId);

public sealed record SignContractResponse(
    Guid Id,
    string Status,
    DateTime ExecutionDate);

public sealed record CreatePricebookLineRequest(
    string? ItemDescription,
    string Sku,
    decimal UnitPrice,
    string Uom,
    string Currency,
    DateTime ValidFrom,
    DateTime ValidTo);

public sealed record PricebookLineResponse(
    Guid Id);

public sealed record IssueAgmtResponse(
    string AgmtId);
