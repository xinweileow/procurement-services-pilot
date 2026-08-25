using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

/// <summary>docs/kb/technical_kb.md Module M2 REST API Listing (Requisitions / Commitment Finalisation).</summary>
[ApiController]
[Route("api/v1/requisitions")]
public sealed class RequisitionsController(ProcurementDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<RequisitionResponse>>> List(
        [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = db.Requisitions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequisitionStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => r.Reference.Contains(search) || r.Title.Contains(search));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RequisitionResponse(
                r.Id, r.Reference, r.RequestId, r.Title, r.Status.ToString(), r.EstCost,
                r.FinalSpent, r.FinalisedOn, r.Notes))
            .ToListAsync(ct);

        return Ok(new PagedResponse<RequisitionResponse>(items, page, pageSize, total));
    }

    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult<RequisitionResponse>> Release(Guid id, CancellationToken ct)
    {
        var req = await db.Requisitions.FindAsync([id], ct)
            ?? throw new NotFoundException($"Requisition {id} not found");

        if (req.Status != RequisitionStatus.AwaitingFinalisation)
        {
            throw new ConflictException($"Requisition {id} is in status '{req.Status}', not 'AwaitingFinalisation'.");
        }

        req.Status = RequisitionStatus.Draft; // released back to draft
        await db.SaveChangesAsync(ct);

        return Ok(new RequisitionResponse(
            req.Id, req.Reference, req.RequestId, req.Title, req.Status.ToString(), req.EstCost,
            req.FinalSpent, req.FinalisedOn, req.Notes));
    }

    [HttpPost("{id:guid}/finalise")]
    public async Task<ActionResult<RequisitionResponse>> Finalise(
        Guid id, [FromBody] FinaliseRequisitionRequest body, CancellationToken ct)
    {
        var req = await db.Requisitions.FindAsync([id], ct)
            ?? throw new NotFoundException($"Requisition {id} not found");

        if (req.Status != RequisitionStatus.AwaitingFinalisation)
        {
            throw new ConflictException($"Requisition {id} is in status '{req.Status}', not 'AwaitingFinalisation'.");
        }

        if (body.FinalSpent <= 0)
        {
            throw new UnprocessableException("Final spent amount must be greater than zero.");
        }

        req.FinalSpent = body.FinalSpent;
        req.FinalisedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        req.Notes = body.Notes;
        req.Status = RequisitionStatus.Finalised;

        await db.SaveChangesAsync(ct);

        return Ok(new RequisitionResponse(
            req.Id, req.Reference, req.RequestId, req.Title, req.Status.ToString(), req.EstCost,
            req.FinalSpent, req.FinalisedOn, req.Notes));
    }
}
