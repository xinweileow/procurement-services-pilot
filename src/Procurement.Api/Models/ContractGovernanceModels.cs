namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M12, Entity: ContractObligation.</summary>
public sealed class ContractObligation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "open"; // open | overdue | closed
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M12, Entity: Dispute.</summary>
public sealed class Dispute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public string? PoNumber { get; set; }
    public string? Grn { get; set; }
    public Guid SupplierId { get; set; }
    public string DisputeType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? FinancialImpact { get; set; }
    public string ResolutionStatus { get; set; } = "open"; // open | resolved | escalated
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M12, Entity: PerformanceRecord.</summary>
public sealed class PerformanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string IncidentDescription { get; set; } = string.Empty;
    public string? CorrectiveAction { get; set; }
    public string ResolutionStatus { get; set; } = "open"; // open | resolved
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
