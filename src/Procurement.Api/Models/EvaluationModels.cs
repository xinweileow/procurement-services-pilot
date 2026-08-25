namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M7, Entity: Evaluation.</summary>
public sealed class Evaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RfxEventId { get; set; }
    public Guid SupplierId { get; set; }
    public string EvaluatorId { get; set; } = string.Empty;
    public decimal TechnicalScore { get; set; }
    public decimal CommercialScore { get; set; }
    public string? Comments { get; set; }
    public string Status { get; set; } = "draft"; // draft | locked
    public DateTime? LockedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M7, Entity: Clarification.</summary>
public sealed class Clarification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RfxEventId { get; set; }
    public Guid SupplierId { get; set; }
    public string Category { get; set; } = "Technical"; // Technical | Commercial | Contract
    public string Question { get; set; } = string.Empty;
    public string? Response { get; set; }
    public bool IsMaterialDeviation { get; set; } = false;
    public string Status { get; set; } = "pending"; // pending | resolved
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}
