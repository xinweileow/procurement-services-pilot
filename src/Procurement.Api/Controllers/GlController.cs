using Microsoft.AspNetCore.Mvc;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/gl-entries")]
public sealed class GlController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<GlEntryResponse>> Create([FromBody] CreateGlEntryRequest body, CancellationToken ct)
    {
        if (body.Debit != body.Credit)
        {
            throw new UnprocessableException(
                $"Debit ({body.Debit}) and credit ({body.Credit}) totals must balance.");
        }

        var entry = new GlEntry
        {
            PaymentId = body.PaymentId,
            GlAccount = body.GlAccount,
            CostCentre = body.CostCentre,
            Entity = body.Entity,
            Debit = body.Debit,
            Credit = body.Credit,
            Tax = body.Tax,
            Wht = body.Wht,
            PostingDate = body.PostingDate,
        };

        db.GlEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        return Ok(new GlEntryResponse(entry.Id));
    }
}
