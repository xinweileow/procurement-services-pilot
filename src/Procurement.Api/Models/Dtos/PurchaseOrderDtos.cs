namespace Procurement.Api.Models.Dtos;

public sealed record PurchaseRequisitionItemRequest(
    string ItemDescription,
    decimal Quantity,
    decimal UnitPrice,
    string? Sku = null,
    string? CostCentre = null,
    string? GlAccount = null);

public sealed record CreatePurchaseRequisitionRequest(
    Guid RequestId,
    IReadOnlyList<PurchaseRequisitionItemRequest> Items,
    Guid? AwardId = null,
    Guid? ContractId = null,
    string? Category = null);

public sealed record PurchaseRequisitionResponse(
    Guid Id,
    string Status);

public sealed record PurchaseRequisitionListItem(
    Guid Id,
    Guid RequestId,
    string? Category,
    string Route,
    string Status,
    string? PoNumber);

public sealed record PagedPurchaseRequisitionResponse(
    IReadOnlyList<PurchaseRequisitionListItem> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record ApprovePurchaseRequisitionResponse(
    Guid Id,
    string Status,
    string PoNumber);

public sealed record RecordGoodsReceiptRequest(
    decimal QuantityReceived,
    DateTime AcceptanceDate,
    string AcceptedBy);

public sealed record GoodsReceiptResponse(
    Guid Id,
    string Grn);

public sealed record CreateInvoiceRequest(
    Guid SupplierId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    decimal Amount,
    decimal Tax,
    string PoNumber,
    string Grn,
    string? Currency = "MYR",
    string? Category = null,
    string? MyInvoisReference = null);

public sealed record InvoiceResponse(
    Guid Id,
    string InvoiceType);

public sealed record CreateEpvVoucherRequest(
    string PoNumber,
    string Grn,
    Guid InvoiceId);

public sealed record EpvVoucherResponse(
    Guid Id,
    string Status);

public sealed record EpvMatchDifferences(
    decimal AmountDifference);

public sealed record EpvMatchResponse(
    string MatchStatus,
    EpvMatchDifferences? Differences);

public sealed record ResolveEpvExceptionRequest(
    string Owner,
    string Resolution,
    string? AdjustmentNote = null);

public sealed record InvoiceExceptionResponse(
    Guid Id,
    string Status);

public sealed record EpvVoucherApproveResponse(
    Guid Id,
    string Status);
