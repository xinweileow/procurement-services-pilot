using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class PurchaseRequisitionsController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost("purchase-requisitions")]
    public async Task<ActionResult<PurchaseRequisitionResponse>> Create(
        [FromBody] CreatePurchaseRequisitionRequest body, CancellationToken ct)
    {
        if (body.Items is null || body.Items.Count == 0)
        {
            throw new UnprocessableException("items must not be empty.");
        }

        var pr = new PurchaseRequisition
        {
            RequestId = body.RequestId,
            AwardId = body.AwardId,
            ContractId = body.ContractId,
            Category = body.Category,
            Route = body.ContractId.HasValue ? "catalogue" : "manual_po",
            Status = "draft",
        };

        foreach (var item in body.Items)
        {
            pr.Items.Add(new PurchaseRequisitionItem
            {
                PurchaseRequisitionId = pr.Id,
                ItemDescription = item.ItemDescription,
                Sku = item.Sku,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                CostCentre = item.CostCentre,
                GlAccount = item.GlAccount,
            });
        }

        db.PurchaseRequisitions.Add(pr);
        await db.SaveChangesAsync(ct);

        return Ok(new PurchaseRequisitionResponse(pr.Id, pr.Status));
    }

    [HttpGet("purchase-requisitions")]
    public async Task<ActionResult<PagedPurchaseRequisitionResponse>> List(
        [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = db.PurchaseRequisitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(p => p.Category == category);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PurchaseRequisitionListItem(p.Id, p.RequestId, p.Category, p.Route, p.Status, p.PoNumber))
            .ToListAsync(ct);

        return Ok(new PagedPurchaseRequisitionResponse(items, page, pageSize, total));
    }

    [HttpPost("purchase-requisitions/{id:guid}/approve")]
    public async Task<ActionResult<ApprovePurchaseRequisitionResponse>> Approve(Guid id, CancellationToken ct)
    {
        var pr = await db.PurchaseRequisitions.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException($"Purchase requisition {id} not found");

        var supplierId = Guid.Empty;

        if (pr.ContractId is { } contractId)
        {
            var contract = await db.Contracts.FindAsync([contractId], ct);
            if (contract is not null)
            {
                if (string.Equals(contract.Status, "expired", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConflictException($"Contract {contractId} linked to purchase requisition {id} has expired.");
                }

                supplierId = contract.SupplierId;

                var skus = pr.Items.Where(i => !string.IsNullOrWhiteSpace(i.Sku)).Select(i => i.Sku).ToList();
                if (skus.Count > 0)
                {
                    var pricebookLines = await db.PricebookLines
                        .Where(l => l.ContractId == contractId && skus.Contains(l.Sku))
                        .ToListAsync(ct);

                    foreach (var item in pr.Items.Where(i => !string.IsNullOrWhiteSpace(i.Sku)))
                    {
                        var line = pricebookLines.FirstOrDefault(l => l.Sku == item.Sku);
                        if (line is not null && line.UnitPrice != item.UnitPrice)
                        {
                            throw new ConflictException(
                                $"Item '{item.ItemDescription}' price {item.UnitPrice} differs from pricebook line price {line.UnitPrice} for SKU {item.Sku}.");
                        }
                    }
                }
            }
        }

        pr.Status = "approved";
        pr.PoNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{pr.Id.ToString()[..8].ToUpperInvariant()}";

        db.PurchaseOrders.Add(new PurchaseOrder
        {
            PurchaseRequisitionId = pr.Id,
            SupplierId = supplierId,
            PoNumber = pr.PoNumber,
            Amount = pr.Items.Sum(i => i.Quantity * i.UnitPrice),
        });

        await db.SaveChangesAsync(ct);

        return Ok(new ApprovePurchaseRequisitionResponse(pr.Id, pr.Status, pr.PoNumber));
    }

    [HttpPost("purchase-orders/{id:guid}/goods-receipts")]
    public async Task<ActionResult<GoodsReceiptResponse>> RecordGoodsReceipt(
        Guid id, [FromBody] RecordGoodsReceiptRequest body, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.FindAsync([id], ct)
            ?? throw new NotFoundException($"Purchase order {id} not found");

        var receipt = new GoodsReceipt
        {
            PurchaseOrderId = po.Id,
            Grn = $"GRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            QuantityReceived = body.QuantityReceived,
            AcceptanceDate = body.AcceptanceDate,
            AcceptedBy = body.AcceptedBy,
        };

        db.GoodsReceipts.Add(receipt);
        await db.SaveChangesAsync(ct);

        return Ok(new GoodsReceiptResponse(receipt.Id, receipt.Grn));
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<InvoiceResponse>> CaptureInvoice([FromBody] CreateInvoiceRequest body, CancellationToken ct)
    {
        var isExempt = string.Equals(body.Category, "Exempt", StringComparison.OrdinalIgnoreCase);
        var myInvoisRequired = !isExempt;

        if (myInvoisRequired && string.IsNullOrWhiteSpace(body.MyInvoisReference))
        {
            throw new UnprocessableException(
                $"Invoice category '{body.Category ?? "(none)"}' requires a MyInvois reference and none was supplied.");
        }

        var currency = string.IsNullOrWhiteSpace(body.Currency) ? "MYR" : body.Currency;

        var duplicate = await db.Invoices.AnyAsync(i =>
            i.SupplierId == body.SupplierId &&
            i.InvoiceNumber == body.InvoiceNumber &&
            i.InvoiceDate == body.InvoiceDate &&
            i.Amount == body.Amount &&
            i.Currency == currency &&
            i.PoNumber == body.PoNumber, ct);

        if (duplicate)
        {
            throw new ConflictException(
                $"An invoice with the same supplier, invoiceNumber, invoiceDate, amount, currency and PO already exists.");
        }

        var invoice = new Invoice
        {
            SupplierId = body.SupplierId,
            InvoiceNumber = body.InvoiceNumber,
            InvoiceDate = body.InvoiceDate,
            Amount = body.Amount,
            Tax = body.Tax,
            Currency = currency,
            PoNumber = body.PoNumber,
            Grn = body.Grn,
            Category = body.Category,
            MyInvoisReference = body.MyInvoisReference,
            InvoiceType = isExempt ? "exempt" : "standard",
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        return Ok(new InvoiceResponse(invoice.Id, invoice.InvoiceType));
    }
}
