namespace Procurement.Api.Models;

/// <summary>
/// docs/kb/technical_kb.md Module M11, Entity: Payment. `PreparerId`, `Category`, `TaxCode`,
/// `WhtAmount`, `SstAmount`, `PayableAmount`, `TaxAmount`, `ApproverId` are not part of the
/// documented Data Model table (only the request schemas mention tax/SoD-adjacent fields) —
/// added so the SoD-conflict-on-approve and applicability-by-category rules are testable.
/// </summary>
public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty;
    public Guid EpvVoucherId { get; set; }
    public string Route { get; set; } = "PO"; // PO | direct | TT
    public Guid PayeeId { get; set; }
    public string? BankDetails { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MYR";
    public DateTime PaymentDate { get; set; }
    public string? PreparerId { get; set; }
    public string? Category { get; set; }
    public string? TaxCode { get; set; }
    public decimal? WhtAmount { get; set; }
    public decimal? SstAmount { get; set; }
    public decimal? PayableAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string? ApproverId { get; set; }
    public string Status { get; set; } = "draft"; // draft | approved | executing | executed | failed | pending
    public string? BankReference { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M11, Entity: SelfBilledRecord.</summary>
public sealed class SelfBilledRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = "not_applicable"; // not_applicable | pending | accepted | rejected | resubmitted
    public string? UniqueId { get; set; }
    public string? Qr { get; set; }
    public string? RejectionReason { get; set; }
    public string? MyInvoisPayloadVersion { get; set; }
    public DateTime? SubmissionDate { get; set; }
}

/// <summary>
/// docs/kb/technical_kb.md Module M11, Entity: GlEntry. `Entity` is not part of the documented
/// Data Model table but is in the REST request schema.
/// </summary>
public sealed class GlEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PaymentId { get; set; }
    public string GlAccount { get; set; } = string.Empty;
    public string CostCentre { get; set; } = string.Empty;
    public string? Entity { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Wht { get; set; }
    public DateTime PostingDate { get; set; }
    public bool ReversalFlag { get; set; } = false;
}

/// <summary>docs/kb/technical_kb.md Module M11, Entity: BankRecord.</summary>
public sealed class BankRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BankReference { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MYR";
    public string Status { get; set; } = "unreconciled";
    public Guid? PaymentId { get; set; }
    public string MatchStatus { get; set; } = "unmatched"; // unmatched | matched
}

/// <summary>
/// Not part of the documented Data Model, added because STORY-M11-3's acceptance criteria
/// requires a reconciliation exception to be created as a side effect of an unmatched bank record.
/// </summary>
public sealed class ReconciliationException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BankRecordId { get; set; }
    public string Status { get; set; } = "open";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M11, Entity: AuditPack.</summary>
public sealed class AuditPack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public DateTime RetentionDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
