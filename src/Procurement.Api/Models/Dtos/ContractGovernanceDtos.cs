namespace Procurement.Api.Models.Dtos;

public sealed record CreateContractObligationRequest(
    string Description,
    string Owner,
    DateTime DueDate);

public sealed record ContractObligationResponse(
    Guid Id,
    string Status);

public sealed record CreateDisputeRequest(
    Guid ContractId,
    Guid SupplierId,
    string DisputeType,
    string Description,
    string? PoNumber = null,
    string? Grn = null,
    decimal? FinancialImpact = null);

public sealed record DisputeResponse(
    Guid Id,
    string Status);

public sealed record CreatePerformanceRecordRequest(
    string PoNumber,
    string IncidentDescription,
    string? CorrectiveAction = null);

public sealed record PerformanceRecordResponse(
    Guid Id);

public sealed record RequalifySupplierRequest(
    string? Trigger = "scheduled"); // scheduled | renewal | material_change

public sealed record RequalifySupplierResponse(
    Guid Id,
    string RequalificationStatus);

public sealed record FinanceValidationRequest(
    bool Validated);

public sealed record FinanceValidationResponse(
    Guid Id,
    bool FinanceValidated);

public sealed record P2PReportResponse(
    Guid RequestId,
    object? Request,
    object? Contract,
    object? Po,
    object? Invoice,
    object? Payment,
    object? Gl,
    string? BankReference,
    IReadOnlyList<object> Exceptions);
