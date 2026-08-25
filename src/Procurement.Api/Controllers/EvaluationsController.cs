using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class EvaluationsController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost("rfx-events/{id:guid}/evaluations")]
    public async Task<ActionResult<EvaluationResponse>> ScoreEvaluation(
        Guid id, [FromBody] ScoreEvaluationRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        var evaluation = await db.Evaluations
            .FirstOrDefaultAsync(e => e.RfxEventId == id && e.SupplierId == body.SupplierId && e.EvaluatorId == body.EvaluatorId, ct);

        if (evaluation != null && string.Equals(evaluation.Status, "locked", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Evaluation is already locked and cannot be modified.");
        }

        if (evaluation is null)
        {
            evaluation = new Evaluation
            {
                RfxEventId = id,
                SupplierId = body.SupplierId,
                EvaluatorId = body.EvaluatorId,
            };
            db.Evaluations.Add(evaluation);
        }

        evaluation.TechnicalScore = body.TechnicalScore;
        evaluation.CommercialScore = body.CommercialScore;
        evaluation.Comments = body.Comments;
        if (body.Lock)
        {
            evaluation.Status = "locked";
            evaluation.LockedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return Ok(new EvaluationResponse(
            evaluation.Id, evaluation.RfxEventId, evaluation.SupplierId, evaluation.EvaluatorId,
            evaluation.TechnicalScore, evaluation.CommercialScore, evaluation.Comments, evaluation.Status, evaluation.LockedAtUtc));
    }

    [HttpPost("rfx-events/{id:guid}/clarifications")]
    public async Task<ActionResult<ClarificationResponse>> RaiseClarification(
        Guid id, [FromBody] RaiseClarificationRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        if (string.IsNullOrWhiteSpace(body.Question))
        {
            throw new UnprocessableException("Clarification question cannot be empty.");
        }

        var clarification = new Clarification
        {
            RfxEventId = id,
            SupplierId = body.SupplierId,
            Category = body.Category,
            Question = body.Question.Trim(),
            IsMaterialDeviation = body.IsMaterialDeviation,
            Status = "pending",
        };

        db.Clarifications.Add(clarification);
        await db.SaveChangesAsync(ct);

        return Ok(new ClarificationResponse(
            clarification.Id, clarification.RfxEventId, clarification.SupplierId, clarification.Category,
            clarification.Question, clarification.Response, clarification.IsMaterialDeviation, clarification.Status,
            clarification.CreatedAtUtc, clarification.ResolvedAtUtc));
    }

    [HttpPatch("clarifications/{id:guid}")]
    public async Task<ActionResult<ClarificationResponse>> ResolveClarification(
        Guid id, [FromBody] ResolveClarificationRequest body, CancellationToken ct)
    {
        var clarification = await db.Clarifications.FindAsync([id], ct)
            ?? throw new NotFoundException($"Clarification {id} not found");

        clarification.Response = body.Response.Trim();
        clarification.Status = "resolved";
        clarification.ResolvedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new ClarificationResponse(
            clarification.Id, clarification.RfxEventId, clarification.SupplierId, clarification.Category,
            clarification.Question, clarification.Response, clarification.IsMaterialDeviation, clarification.Status,
            clarification.CreatedAtUtc, clarification.ResolvedAtUtc));
    }

    [HttpPost("rfx-events/{id:guid}/status-gate-decision")]
    public async Task<ActionResult<StatusGateDecisionResponse>> StatusGateDecision(
        Guid id, [FromBody] StatusGateDecisionRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        if (string.Equals(body.Decision, "proceed", StringComparison.OrdinalIgnoreCase))
        {
            var hasUnresolvedMaterialDeviation = await db.Clarifications
                .AnyAsync(c => c.RfxEventId == id && c.IsMaterialDeviation && c.Status != "resolved", ct);

            if (hasUnresolvedMaterialDeviation)
            {
                throw new UnprocessableException(
                    "Cannot pass status-gate decision while unresolved material clarifications or deviations exist.");
            }
        }

        return Ok(new StatusGateDecisionResponse(
            id, body.Decision, true, "Status-gate decision approved to proceed."));
    }
}
