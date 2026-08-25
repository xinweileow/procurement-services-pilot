namespace Procurement.Api.Models;

public enum RequestStatus
{
    Draft,
    Submitted,
    Validated,
    BudgetHold,
    Approved,
    Rejected,
    Cancelled,
}

public enum RoutingDestination
{
    None,
    EtiqaInternalProcurement,
    Gsp,
}

/// <summary>
/// docs/kb/technical_kb.md Module M1, Entity: Request.
/// </summary>
public sealed class Request
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>System-generated display id, e.g. PR-2026-000123.</summary>
    public string RequestId { get; set; } = string.Empty;

    public string RequesterId { get; set; } = string.Empty;
    public string RequesterRole { get; set; } = string.Empty; // IT Business Requestor | Non-IT Business Requestor | Procurement Administrator
    public string BusinessUnit { get; set; } = string.Empty;
    public string CostCentre { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "MYR";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Country { get; set; } = "Malaysia";
    public string Entity { get; set; } = string.Empty;
    public DateOnly DeliveryDate { get; set; }
    public string Criticality { get; set; } = "Standard";

    public string EngagementPathway { get; set; } = string.Empty; // Sourcing with Contract | Sourcing Only | Contract Only | Other Query
    public string ProcurementNature { get; set; } = "New Procurement";
    public string? PreviousContractId { get; set; }

    public bool IsNonCatalogue { get; set; }

    public bool DeclarationNoConflict { get; set; }
    public bool DeclarationConnectedPartyDeclared { get; set; }
    public bool DeclarationNoSplitting { get; set; }
    public bool DeclarationComplete { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    public RoutingDestination RoutingDestination { get; set; } = RoutingDestination.None;

    /// <summary>Set on submit when a duplicate/anti-splitting match is found (docs/kb/business_kb.md
    /// Module M1, S.1.8) — flagged for Procurement review, never blocks submission itself.</summary>
    public bool PotentialDuplicateOrSplit { get; set; }

    public DateTime SubmittedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<RequestAttachment> Attachments { get; set; } = [];
}
