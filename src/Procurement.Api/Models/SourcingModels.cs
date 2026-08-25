namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M4, Entity: SourcingStrategy.</summary>
public sealed class SourcingStrategy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string RecommendedRoute { get; set; } = "RFQ"; // RFQ | RFP | SingleSource | Renewal
    public string? MarketApproach { get; set; }
    public string? EvaluationMethod { get; set; }
    public string Status { get; set; } = "draft"; // draft | agreed
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M4, Entity: ThreePointCheck.</summary>
public sealed class ThreePointCheck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierId { get; set; }
    public bool IdentityVerified { get; set; }
    public bool RegistrationEvidenceVerified { get; set; }
    public bool ActiveStatus { get; set; }
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M5, Entity: Supplier.</summary>
public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LegalEntityName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string KycStatus { get; set; } = "completed"; // pending | completed | failed
    public int EsgScore { get; set; } = 75;
    public bool ActiveFlag { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M1 / M4, Entity: TriageDecision.</summary>
public sealed class TriageDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string Decision { get; set; } = "accept"; // accept | return | reject | assign
    public string? Reason { get; set; }
    public string? AssigneeTeam { get; set; }
    public string DecidedBy { get; set; } = "Procurement Administrator";
    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;
}
