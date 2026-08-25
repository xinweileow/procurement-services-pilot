using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/epv-vouchers")]
public sealed class EpvVouchersController(ProcurementDbContext db) : ControllerBase
{
    private static readonly string[] ValidResolutions = ["correction", "dispute", "credit_note", "debit_note", "refund_note"];
    private const decimal MatchTolerance = 0.01m;

    [HttpPost]
    public async Task<ActionResult<EpvVoucherResponse>> Create([FromBody] CreateEpvVoucherRequest body, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.FirstOrDefaultAsync(p => p.PoNumber == body.PoNumber, ct)
            ?? throw new NotFoundException($"Purchase order '{body.PoNumber}' not found");

        var gr = await db.GoodsReceipts.FirstOrDefaultAsync(g => g.Grn == body.Grn, ct)
            ?? throw new NotFoundException($"Goods receipt '{body.Grn}' not found");

        var invoice = await db.Invoices.FindAsync([body.InvoiceId], ct)
            ?? throw new NotFoundException($"Invoice {body.InvoiceId} not found");

        var voucher = new EpvVoucher
        {
            PoNumber = po.PoNumber,
            Grn = gr.Grn,
            InvoiceId = invoice.Id,
            Status = "pending_match",
        };

        db.EpvVouchers.Add(voucher);
        await db.SaveChangesAsync(ct);

        return Ok(new EpvVoucherResponse(voucher.Id, voucher.Status));
    }

    [HttpPost("{id:guid}/match")]
    public async Task<ActionResult<EpvMatchResponse>> Match(Guid id, CancellationToken ct)
    {
        var voucher = await db.EpvVouchers.FindAsync([id], ct)
            ?? throw new NotFoundException($"EPV voucher {id} not found");

        var po = await db.PurchaseOrders.FirstOrDefaultAsync(p => p.PoNumber == voucher.PoNumber, ct);
        var invoice = await db.Invoices.FindAsync([voucher.InvoiceId], ct);

        var amountDifference = (invoice?.Amount ?? 0m) - (po?.Amount ?? 0m);

        if (Math.Abs(amountDifference) <= MatchTolerance)
        {
            voucher.Status = "matched";
            await db.SaveChangesAsync(ct);
            return Ok(new EpvMatchResponse("matched", null));
        }

        voucher.Status = "mismatch";
        db.InvoiceExceptions.Add(new InvoiceException
        {
            EpvVoucherId = voucher.Id,
            Status = "open",
        });
        await db.SaveChangesAsync(ct);

        return Ok(new EpvMatchResponse("mismatch", new EpvMatchDifferences(amountDifference)));
    }

    [HttpPatch("{id:guid}/exceptions/{exceptionId:guid}")]
    public async Task<ActionResult<InvoiceExceptionResponse>> ResolveException(
        Guid id, Guid exceptionId, [FromBody] ResolveEpvExceptionRequest body, CancellationToken ct)
    {
        var exception = await db.InvoiceExceptions.FirstOrDefaultAsync(e => e.Id == exceptionId && e.EpvVoucherId == id, ct)
            ?? throw new NotFoundException($"Invoice exception {exceptionId} not found for EPV voucher {id}");

        if (!ValidResolutions.Contains(body.Resolution, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnprocessableException(
                $"resolution must be one of: {string.Join(", ", ValidResolutions)}.");
        }

        exception.Owner = body.Owner;
        exception.Resolution = body.Resolution;
        exception.AdjustmentNote = body.AdjustmentNote;
        exception.Status = "resolved";

        await db.SaveChangesAsync(ct);

        return Ok(new InvoiceExceptionResponse(exception.Id, exception.Status));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<EpvVoucherApproveResponse>> Approve(Guid id, CancellationToken ct)
    {
        var voucher = await db.EpvVouchers.FindAsync([id], ct)
            ?? throw new NotFoundException($"EPV voucher {id} not found");

        if (!string.Equals(voucher.Status, "matched", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"EPV voucher {id} is not matched (status: {voucher.Status}); cannot approve.");
        }

        voucher.Status = "ready_for_payment";
        await db.SaveChangesAsync(ct);

        return Ok(new EpvVoucherApproveResponse(voucher.Id, voucher.Status));
    }
}
