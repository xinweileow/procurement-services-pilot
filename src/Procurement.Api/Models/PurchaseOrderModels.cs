namespace Procurement.Api.Models;

/// <summary>docs/kb/technical_kb.md Module M10, Entity: PurchaseRequisition.</summary>
public sealed class PurchaseRequisition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public Guid? AwardId { get; set; }
    public Guid? ContractId { get; set; }
    public string? Category { get; set; }
    public string Route { get; set; } = "manual_po"; // catalogue | non_catalogue | manual_po
    public string Status { get; set; } = "draft"; // draft | approved | rejected
    public string? PoNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<PurchaseRequisitionItem> Items { get; set; } = [];
}

/// <summary>
/// Not part of the KB's documented Data Model table (only the request-schema shape is
/// given for items) — added as a child entity so items persist between create and approve.
/// </summary>
public sealed class PurchaseRequisitionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseRequisitionId { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? CostCentre { get; set; }
    public string? GlAccount { get; set; }
}

/// <summary>docs/kb/technical_kb.md Module M10, Entity: PurchaseOrder.</summary>
public sealed class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseRequisitionId { get; set; }
    public Guid SupplierId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public DateTime PoDate { get; set; } = DateTime.UtcNow.Date;
    public decimal Amount { get; set; }
}

/// <summary>docs/kb/technical_kb.md Module M10, Entity: GoodsReceipt.</summary>
public sealed class GoodsReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public string Grn { get; set; } = string.Empty;
    public decimal QuantityReceived { get; set; }
    public DateTime AcceptanceDate { get; set; }
    public string AcceptedBy { get; set; } = string.Empty;
}

/// <summary>
/// docs/kb/technical_kb.md Module M10, Entity: Invoice. `Currency` and `Category` are not part
/// of the documented Data Model table but are required to make the documented duplicate-check
/// (which explicitly compares currency) and MyInvois-applicability-by-category rule testable.
/// </summary>
public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public string Currency { get; set; } = "MYR";
    public string PoNumber { get; set; } = string.Empty;
    public string Grn { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? MyInvoisReference { get; set; }
    public string InvoiceType { get; set; } = "standard"; // standard | self_billed_pending | exempt
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/kb/technical_kb.md Module M10, Entity: EpvVoucher.</summary>
public sealed class EpvVoucher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PoNumber { get; set; } = string.Empty;
    public string Grn { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public string Status { get; set; } = "pending_match"; // pending_match | matched | mismatch | ready_for_payment | approved
}

/// <summary>docs/kb/technical_kb.md Module M10, Entity: InvoiceException.</summary>
public sealed class InvoiceException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EpvVoucherId { get; set; }
    public string? Owner { get; set; }
    public string? Resolution { get; set; } // correction | dispute | credit_note | debit_note | refund_note
    public string? AdjustmentNote { get; set; }
    public string Status { get; set; } = "open"; // open | resolved
}
