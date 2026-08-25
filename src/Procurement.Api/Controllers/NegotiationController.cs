using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class NegotiationController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost("rfx-events/{id:guid}/negotiation-rounds")]
    public async Task<ActionResult<NegotiationRoundResponse>> RecordNegotiationRound(
        Guid id, [FromBody] RecordNegotiationRoundRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        var roundType = body.RoundType?.ToLowerInvariant() == "eauction" ? "eAuction" : "negotiation";

        var round = new NegotiationRound
        {
            RfxEventId = rfx.Id,
            SupplierId = body.SupplierId,
            RoundNumber = await NextRoundNumberAsync(rfx.Id, body.SupplierId, ct),
            RevisedPrice = body.RevisedPrice,
            Terms = body.Terms,
            RoundType = roundType,
            Status = "recorded",
        };

        db.NegotiationRounds.Add(round);
        await db.SaveChangesAsync(ct);

        return Ok(new NegotiationRoundResponse(round.Id, round.RoundNumber));
    }

    [HttpPost("rfx-events/{id:guid}/bafo-requests")]
    public async Task<ActionResult<BafoRequestResponse>> RequestBafo(
        Guid id, [FromBody] RequestBafoRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        var round = new NegotiationRound
        {
            RfxEventId = rfx.Id,
            SupplierId = body.SupplierId,
            RoundNumber = await NextRoundNumberAsync(rfx.Id, body.SupplierId, ct),
            RoundType = "bafo",
            Status = "requested",
            Deadline = body.Deadline,
        };

        db.NegotiationRounds.Add(round);
        await db.SaveChangesAsync(ct);

        return Ok(new BafoRequestResponse(round.Id, round.Status));
    }

    [HttpGet("rfx-events/{id:guid}/consolidated-result")]
    public async Task<ActionResult<List<ConsolidatedResultItem>>> GetConsolidatedResult(
        Guid id, [FromQuery] Guid? supplierId, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        var evaluationsQuery = db.Evaluations.Where(e => e.RfxEventId == id);
        if (supplierId.HasValue)
        {
            evaluationsQuery = evaluationsQuery.Where(e => e.SupplierId == supplierId.Value);
        }

        var evaluations = await evaluationsQuery.ToListAsync(ct);

        if (supplierId.HasValue && evaluations.Count == 0)
        {
            throw new NotFoundException($"No evaluations found for supplier {supplierId} on RFx event {id}");
        }

        var bySupplier = evaluations.GroupBy(e => e.SupplierId);

        var notAllLocked = bySupplier.Any(g => g.Any(e => !string.Equals(e.Status, "locked", StringComparison.OrdinalIgnoreCase)));
        if (notAllLocked)
        {
            throw new ConflictException("Cannot consolidate results while one or more evaluation dimensions are not yet locked.");
        }

        var results = bySupplier
            .Select(g => new ConsolidatedResultItem(
                g.Key,
                g.Average(e => e.TechnicalScore + e.CommercialScore),
                "single_award"))
            .ToList();

        return Ok(results);
    }

    [HttpPost("rfx-events/{id:guid}/savings-calculation")]
    public async Task<ActionResult<SavingsCalculationResponse>> CalculateSavings(
        Guid id, [FromBody] SavingsCalculationRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        if (body.BaselineValue is null)
        {
            throw new UnprocessableException("baselineValue is required to calculate savings against the approved baseline.");
        }

        var savingsType = body.Method?.ToLowerInvariant() == "soft" ? "soft" : "hard";

        var record = new SavingsRecord
        {
            RfxEventId = rfx.Id,
            BaselineValue = body.BaselineValue.Value,
            AwardedValue = body.AwardedValue,
            SavingsType = savingsType,
            FinanceValidated = false,
        };

        db.SavingsRecords.Add(record);
        await db.SaveChangesAsync(ct);

        return Ok(new SavingsCalculationResponse(record.SavingsAmount, record.SavingsType, record.FinanceValidated));
    }

    private async Task<int> NextRoundNumberAsync(Guid rfxEventId, Guid supplierId, CancellationToken ct)
    {
        var lastRoundNumber = await db.NegotiationRounds
            .Where(r => r.RfxEventId == rfxEventId && r.SupplierId == supplierId)
            .Select(r => (int?)r.RoundNumber)
            .MaxAsync(ct);

        return (lastRoundNumber ?? 0) + 1;
    }
}
