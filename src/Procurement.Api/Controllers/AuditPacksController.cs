using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/audit-packs")]
public sealed class AuditPacksController(ProcurementDbContext db) : ControllerBase
{
    private const int RetentionYears = 15;

    [HttpPost]
    public async Task<ActionResult<AuditPackResponse>> Create([FromBody] CreateAuditPackRequest body, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([body.RequestId], ct)
            ?? throw new NotFoundException($"Request {body.RequestId} not found");

        var auditPack = new AuditPack
        {
            RequestId = request.Id,
            RetentionDate = DateTime.UtcNow.Date.AddYears(RetentionYears),
        };

        db.AuditPacks.Add(auditPack);
        await db.SaveChangesAsync(ct);

        return Ok(new AuditPackResponse(auditPack.Id, auditPack.RetentionDate));
    }

    /// <summary>docs/kb/technical_kb.md Module M12 REST API Listing: end-to-end P2P report, assembling the linked chain from request through bank reconciliation.</summary>
    [HttpGet("{requestId:guid}/p2p-report")]
    public async Task<ActionResult<P2PReportResponse>> GetP2PReport(Guid requestId, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([requestId], ct)
            ?? throw new NotFoundException($"Request {requestId} not found");

        var pr = await db.PurchaseRequisitions.FirstOrDefaultAsync(p => p.RequestId == requestId, ct);
        var po = pr is null ? null : await db.PurchaseOrders.FirstOrDefaultAsync(p => p.PurchaseRequisitionId == pr.Id, ct);
        var invoice = po is null ? null : await db.Invoices.FirstOrDefaultAsync(i => i.PoNumber == po.PoNumber, ct);
        var epvVoucher = po is null ? null : await db.EpvVouchers.FirstOrDefaultAsync(v => v.PoNumber == po.PoNumber, ct);
        var payment = epvVoucher is null ? null : await db.Payments.FirstOrDefaultAsync(p => p.EpvVoucherId == epvVoucher.Id, ct);
        var glEntry = payment is null ? null : await db.GlEntries.FirstOrDefaultAsync(g => g.PaymentId == payment.Id, ct);
        var bankRecord = payment is null ? null : await db.BankRecords.FirstOrDefaultAsync(b => b.PaymentId == payment.Id, ct);

        var exceptions = new List<object>();
        if (epvVoucher is not null)
        {
            exceptions.AddRange(await db.InvoiceExceptions.Where(e => e.EpvVoucherId == epvVoucher.Id).ToListAsync(ct));
        }
        if (bankRecord is not null)
        {
            exceptions.AddRange(await db.ReconciliationExceptions.Where(e => e.BankRecordId == bankRecord.Id).ToListAsync(ct));
        }

        return Ok(new P2PReportResponse(
            requestId,
            request,
            po is null ? null : await db.Contracts.FindAsync([pr!.ContractId ?? Guid.Empty], ct),
            po,
            invoice,
            payment,
            glEntry,
            bankRecord?.BankReference ?? payment?.BankReference,
            exceptions));
    }
}
