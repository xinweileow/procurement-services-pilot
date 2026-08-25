namespace Procurement.Api.Models.Dtos;

public sealed record CreateRfxEventRequest(
    Guid SourcingStrategyId,
    Guid? TechnicalTemplateId = null,
    Guid? CommercialTemplateId = null,
    Guid? ContractTemplateId = null,
    string TenderType = "open");

public sealed record RfxEventResponse(
    Guid Id,
    Guid SourcingStrategyId,
    string TenderType,
    string Status,
    DateTime? OpeningDateUtc,
    DateTime? ClosingDateUtc,
    DateTime CreatedAtUtc);

public sealed record PublishRfxRequest(
    DateTime? OpeningDateUtc = null,
    DateTime? ClosingDateUtc = null);

public sealed record PublishRfxResponse(
    Guid Id,
    string Status,
    DateTime OpeningDate,
    DateTime ClosingDate);

public sealed record InviteSuppliersRequest(
    IReadOnlyList<Guid> SupplierIds);

public sealed record InviteSuppliersResponse(
    IReadOnlyList<Guid> Invited);

public sealed record ExtendRfxDeadlineRequest(
    DateTime NewClosingDate,
    string Reason);

public sealed record ExtendRfxDeadlineResponse(
    Guid Id,
    DateTime ClosingDate);

public sealed record SubmitProposalRequest(
    Guid SupplierId,
    string TechnicalProposal,
    string CommercialProposal,
    bool LateExceptionGranted = false);

public sealed record SubmitProposalResponse(
    Guid Id,
    DateTime SubmittedAt,
    string BidStatus);

public sealed record OpenProposalsResponse(
    string OpenedBy,
    DateTime OpenedAt,
    IReadOnlyList<RfxSubmissionResponse> Submissions);

public sealed record RfxSubmissionResponse(
    Guid Id,
    Guid SupplierId,
    string? TechnicalProposal,
    string? CommercialProposal,
    string BidStatus,
    DateTime SubmittedAt);

public sealed record CancelRfxRequest(
    string Reason);
