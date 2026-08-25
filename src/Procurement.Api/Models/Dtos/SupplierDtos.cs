namespace Procurement.Api.Models.Dtos;

public sealed record CreateSupplierRequest(
    string LegalEntityName,
    string RegistrationNumber,
    string TaxId,
    string Address,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? BeneficiaryName = null,
    string? BankName = null,
    string? AccountNumber = null,
    string? PaymentTerms = "Net 30",
    string? CategoryCodes = "IT,General");

public sealed record SupplierResponse(
    Guid Id,
    string LegalEntityName,
    string RegistrationNumber,
    string TaxId,
    string Address,
    string Status,
    bool ActiveFlag);

public sealed record SupplierStatusResponse(
    Guid Id,
    bool Active,
    string ThreePCStatus,
    int EsgScore,
    string AssociatedPersonStatus,
    string MaterialityStatus);

public sealed record DuplicateCheckRequest(
    string LegalEntityName,
    string? RegistrationNumber = null,
    string? TaxId = null,
    string? BankAccountNumber = null,
    string? ContactEmail = null);

public sealed record DuplicateCheckResponse(
    bool DuplicateFound,
    IReadOnlyList<SupplierCandidateDto> Candidates);

public sealed record SupplierCandidateDto(
    Guid Id,
    string LegalEntityName,
    string RegistrationNumber,
    string TaxId,
    bool ActiveFlag,
    string MatchReason);

public sealed record CreateSupplierExceptionRequest(
    string Reason,
    Guid? ProcurementHeadApprovalAttachmentId);

public sealed record SupplierExceptionResponse(
    Guid Id,
    Guid RequestId,
    string Reason,
    Guid ProcurementHeadApprovalAttachmentId,
    string Status);

public sealed record UpdateBankDetailsRequest(
    string BeneficiaryName,
    string BankName,
    string AccountNumber);

public sealed record BankDetailsResponse(
    Guid Id,
    string Status,
    string Message);

public sealed record DueDiligenceResponse(
    Guid Id,
    Guid RequestId,
    string SourcingType,
    string SpendType,
    bool ThreePCRequired,
    bool EsgRequired,
    string ThreePCStatus,
    int? EsgScore,
    string AssociatedPersonStatus,
    string TprmStatus,
    string MaterialityStatus,
    bool Ready);

public sealed record UpdateDueDiligenceApplicabilityRequest(
    string SourcingType,
    string SpendType,
    string? MaterialityStatus = null);

public sealed record UpdateSupplierProfileRequest(
    string? Address = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? PaymentTerms = null,
    string? CategoryCodes = null);

public sealed record ExpiringDocumentDto(
    Guid SupplierId,
    string SupplierName,
    string DocumentType,
    string FileName,
    DateTime ExpiryDateUtc,
    int DaysUntilExpiry);
