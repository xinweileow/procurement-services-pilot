namespace Procurement.Api.Models.Dtos;

public sealed record CreatePaymentRequest(
    Guid EpvVoucherId,
    string Route, // PO | direct | TT
    Guid PayeeId,
    string? BankDetails,
    decimal Amount,
    string Currency,
    DateTime PaymentDate,
    string? PreparerId = null,
    string? Category = null);

public sealed record PaymentResponse(
    Guid Id,
    string Reference,
    string Status);

public sealed record TaxTreatmentRequest(
    string TaxCode,
    decimal? WhtAmount = null,
    decimal? SstAmount = null);

public sealed record TaxTreatmentResponse(
    Guid Id,
    decimal PayableAmount,
    decimal TaxAmount);

public sealed record ApprovePaymentRequest(
    string ApproverId);

public sealed record ApprovePaymentResponse(
    Guid Id,
    string Status);

public sealed record PaymentControlsCheckResponse(
    bool BeneficiaryVerified,
    bool DuplicateFlag,
    bool WithinLimit);

public sealed record ExecutePaymentResponse(
    Guid Id,
    string Status,
    string? BankReference);

public sealed record SelfBilledApplicabilityResponse(
    bool Applicable);

public sealed record SelfBilledSubmissionResponse(
    Guid Id,
    string MyInvoisPayloadVersion,
    DateTime SubmissionDate);

public sealed record MyInvoisWebhookRequest(
    Guid SelfBilledRecordId,
    string? UniqueId,
    string? Qr,
    string Status, // accepted | rejected
    string? RejectionReason = null);

public sealed record MyInvoisWebhookResponse(
    bool Received);

public sealed record ResubmitSelfBilledRequest(
    Dictionary<string, string>? CorrectedFields = null);

public sealed record ResubmitSelfBilledResponse(
    Guid Id,
    string Status);

public sealed record CreateGlEntryRequest(
    string GlAccount,
    string CostCentre,
    string? Entity,
    decimal Debit,
    decimal Credit,
    decimal? Tax,
    decimal? Wht,
    DateTime PostingDate,
    Guid? PaymentId = null);

public sealed record GlEntryResponse(
    Guid Id);

public sealed record CreateBankRecordRequest(
    string BankReference,
    DateTime Date,
    decimal Amount,
    string Currency,
    string? Status = null);

public sealed record BankRecordResponse(
    Guid Id);

public sealed record MatchBankRecordRequest(
    Guid? PaymentId);

public sealed record MatchBankRecordResponse(
    string MatchStatus);

public sealed record CreateAuditPackRequest(
    Guid RequestId);

public sealed record AuditPackResponse(
    Guid Id,
    DateTime RetentionDate);
