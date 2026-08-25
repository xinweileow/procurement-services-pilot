namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M5, Entity: Supplier.</summary>
public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LegalEntityName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public bool BankVerified { get; set; } = false;
    public string? BankStatus { get; set; } = "pending_verification"; // pending_verification | verified | rejected
    public string? PaymentTerms { get; set; } = "Net 30";
    public string CategoryCodes { get; set; } = "IT,General";
    public string KycStatus { get; set; } = "completed"; // pending | completed | failed
    public int EsgScore { get; set; } = 75;
    public string RiskLevel { get; set; } = "Low"; // Low | Medium | High
    public string SpendTier { get; set; } = "Tier 1";
    public bool ActiveFlag { get; set; } = true;
    public DateTime? LicenseExpiryUtc { get; set; }
    public DateTime? RequalificationDueUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M5, Entity: DueDiligenceRecord.</summary>
public sealed class DueDiligenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public Guid SupplierId { get; set; }
    public string SourcingType { get; set; } = "Sourceable"; // Sourceable | Non-Sourceable
    public string SpendType { get; set; } = "Addressable Spend"; // Addressable Spend | Non-Addressable Spend
    public string ThreePCStatus { get; set; } = "Valid"; // Valid | Invalid | Pending | Not Applicable
    public int? EsgScore { get; set; } = 75;
    public string AssociatedPersonStatus { get; set; } = "Completed"; // Completed | Pending
    public string TprmStatus { get; set; } = "Completed"; // Completed | Pending | Not Applicable
    public string MaterialityStatus { get; set; } = "Non-Material"; // Material | Non-Material | To be assessed
    public bool MaterialSupplierFormAttached { get; set; } = true;
    public bool MaterialSupplierDocsAttached { get; set; } = true;
    public bool TprmEvidenceAttached { get; set; } = true;
    public bool ThreePCEvidenceAttached { get; set; } = true;
    public bool EsgEvidenceAttached { get; set; } = true;
    public bool AssociatedPersonEvidenceAttached { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M5, Entity: SupplierException.</summary>
public sealed class SupplierException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ProcurementHeadApprovalAttachmentId { get; set; }
    public string Status { get; set; } = "pending_approval"; // pending_approval | approved | rejected
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M5, Document tracking entity.</summary>
public sealed class SupplierDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierId { get; set; }
    public string DocumentType { get; set; } = "CompanyRegistration"; // CompanyRegistration | TaxCertificate | BankStatement | ESGCertificate
    public string FileName { get; set; } = string.Empty;
    public DateTime ExpiryDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
