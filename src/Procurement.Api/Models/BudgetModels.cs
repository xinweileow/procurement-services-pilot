namespace Procurement.Api.Models;

public enum CostType
{
    Capex,
    Opex,
}

public enum CommitmentStatus
{
    Reserved,
    Released,
    Consumed,
}

public enum RequisitionStatus
{
    Draft,
    Submitted,
    Validated,
    BudgetHold,
    Approved,
    AwaitingFinalisation,
    Finalised,
    Rejected,
    Cancelled,
}

public enum BudgetExceptionStatus
{
    PendingApproval,
    Approved,
    Rejected,
}

/// <summary>docs/kb/technical_kb.md Module M2, Entity: Budget.</summary>
public sealed class Budget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int FiscalYear { get; set; }
    public string Entity { get; set; } = "Etiqa";
    public string BusinessUnit { get; set; } = string.Empty;
    public string CostCentre { get; set; } = string.Empty;
    public string GlAccount { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public string Currency { get; set; } = "MYR";
    public decimal AllocatedAmount { get; set; }
    public decimal ActualSpend { get; set; }
    public CostType CostType { get; set; } = CostType.Capex;

    public List<BudgetCommitment> Commitments { get; set; } = [];
}

/// <summary>docs/kb/technical_kb.md Module M2, Entity: BudgetCommitment.</summary>
public sealed class BudgetCommitment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BudgetId { get; set; }
    public Guid RequestId { get; set; }
    public decimal CommittedAmount { get; set; }
    public CommitmentStatus Status { get; set; } = CommitmentStatus.Reserved;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M2, Entity: BudgetException.</summary>
public sealed class BudgetException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public decimal Shortfall { get; set; }
    public string Reason { get; set; } = string.Empty;
    public BudgetExceptionStatus Status { get; set; } = BudgetExceptionStatus.PendingApproval;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M2, Entity: Requisition.</summary>
public sealed class Requisition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty; // REQ-2026-001
    public Guid RequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;
    public decimal EstCost { get; set; }
    public decimal? FinalSpent { get; set; }
    public DateOnly? FinalisedOn { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
