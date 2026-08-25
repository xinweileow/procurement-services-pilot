namespace Procurement.Api.Models.Dtos;

public sealed record BudgetSummaryResponse(
    decimal Allocated,
    decimal Reserved,
    decimal ActualSpend,
    decimal Available,
    decimal UtilisationPercentage,
    IReadOnlyList<CostCentreBreakdown> ByCostCentre,
    IReadOnlyList<MonthlyBreakdown> ByMonth);

public sealed record CostCentreBreakdown(
    string CostCentre,
    decimal CapexBalance,
    decimal OpexBalance,
    decimal UtilisationPercentage);

public sealed record MonthlyBreakdown(
    int Month,
    decimal Capex,
    decimal Opex,
    decimal Total);

public sealed class BudgetAvailabilityCheckRequest
{
    public string CostCentre { get; set; } = string.Empty;
    public string GlAccount { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public decimal RequestedAmount { get; set; }
    public string Currency { get; set; } = "MYR";
}

public sealed record BudgetAvailabilityCheckResponse(
    bool Sufficient,
    decimal AvailableBalance,
    decimal Shortfall);

public sealed class CreateBudgetExceptionRequest
{
    public Guid RequestId { get; set; }
    public decimal Shortfall { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed record BudgetExceptionResponse(Guid Id, Guid RequestId, decimal Shortfall, string Status);

public sealed class FinaliseRequisitionRequest
{
    public decimal FinalSpent { get; set; }
    public string? Notes { get; set; }
}

public sealed record RequisitionResponse(
    Guid Id,
    string Reference,
    Guid RequestId,
    string Title,
    string Status,
    decimal EstCost,
    decimal? FinalSpent,
    DateOnly? FinalisedOn,
    string? Notes);
