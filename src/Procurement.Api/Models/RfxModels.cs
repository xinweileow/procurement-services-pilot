namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M6, Entity: RfxEvent.</summary>
public sealed class RfxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourcingStrategyId { get; set; }
    public Guid? TechnicalTemplateId { get; set; }
    public Guid? CommercialTemplateId { get; set; }
    public Guid? ContractTemplateId { get; set; }
    public string TenderType { get; set; } = "open"; // open | invited
    public string Status { get; set; } = "draft"; // draft | published | closed | cancelled | opened
    public DateTime? OpeningDateUtc { get; set; }
    public DateTime? ClosingDateUtc { get; set; }
    public string? ExtensionReason { get; set; }
    public string? CancellationReason { get; set; }
    public string? OpenedBy { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M6, Entity: RfxInvitation.</summary>
public sealed class RfxInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RfxEventId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime InvitedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M6, Entity: RfxSubmission.</summary>
public sealed class RfxSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RfxEventId { get; set; }
    public Guid SupplierId { get; set; }
    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }
    public string BidStatus { get; set; } = "submitted"; // submitted | late | opened | disqualified
    public bool LateExceptionGranted { get; set; } = false;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}
