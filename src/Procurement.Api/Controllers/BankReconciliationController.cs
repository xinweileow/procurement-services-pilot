using Microsoft.AspNetCore.Mvc;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/bank-records")]
public sealed class BankReconciliationController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BankRecordResponse>> Create([FromBody] CreateBankRecordRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.BankReference))
        {
            throw new UnprocessableException("bankReference is required.");
        }

        var record = new BankRecord
        {
            BankReference = body.BankReference,
            Date = body.Date,
            Amount = body.Amount,
            Currency = body.Currency,
            Status = string.IsNullOrWhiteSpace(body.Status) ? "unreconciled" : body.Status,
            MatchStatus = "unmatched",
        };

        db.BankRecords.Add(record);
        await db.SaveChangesAsync(ct);

        return Ok(new BankRecordResponse(record.Id));
    }

    [HttpPost("{id:guid}/match")]
    public async Task<ActionResult<MatchBankRecordResponse>> Match(
        Guid id, [FromBody] MatchBankRecordRequest body, CancellationToken ct)
    {
        var record = await db.BankRecords.FindAsync([id], ct)
            ?? throw new NotFoundException($"Bank record {id} not found");

        var payment = body.PaymentId.HasValue
            ? await db.Payments.FindAsync([body.PaymentId.Value], ct)
            : null;

        if (payment is not null)
        {
            record.PaymentId = payment.Id;
            record.MatchStatus = "matched";
            await db.SaveChangesAsync(ct);
            return Ok(new MatchBankRecordResponse(record.MatchStatus));
        }

        record.MatchStatus = "unmatched";
        db.ReconciliationExceptions.Add(new ReconciliationException
        {
            BankRecordId = record.Id,
            Status = "open",
        });
        await db.SaveChangesAsync(ct);

        return Ok(new MatchBankRecordResponse(record.MatchStatus));
    }
}
