namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M9, Entity: Award.</summary>
public sealed class Award
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RfxEventId { get; set; }
    public string? ConsolidatedResultRef { get; set; }
    public Guid RecommendedSupplierId { get; set; }
    public string? Scenario { get; set; }
    public string Status { get; set; } = "pending_approval"; // pending_approval | approved | rejected
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// docs/kb/technical_kb.md Module M9, Entity: Contract. `Value` is not part of the documented
/// Data Model table but is required to make signatory-authority-limit enforcement on
/// POST /contracts/{id}/sign testable — accepted as an extension on CreateContractRequest,
/// carried over as a plain field here (Module M9's Open Decisions already flag ownership/
/// thresholds for this feature as unconfirmed by the source).
/// </summary>
public sealed class Contract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? AgmtId { get; set; }
    public Guid AwardId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? TemplateId { get; set; }
    public decimal Value { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Status { get; set; } = "drafting"; // drafting | redlining | signed | active | expired | terminated
    public DateTime? ExecutionDate { get; set; }
    public DateTime EffectiveStart { get; set; }
    public DateTime EffectiveEnd { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M9, Entity: PricebookLine.</summary>
public sealed class PricebookLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public string? ItemDescription { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Uom { get; set; } = string.Empty;
    public string Currency { get; set; } = "MYR";
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}
