using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/awards")]
public sealed class AwardsController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AwardResponse>> Create([FromBody] CreateAwardRequest body, CancellationToken ct)
    {
        if (body.RecommendedSupplierId == Guid.Empty)
        {
            throw new UnprocessableException("recommendedSupplierId is required.");
        }

        var rfx = await db.RfxEvents.FindAsync([body.RfxEventId], ct)
            ?? throw new NotFoundException($"RFx event {body.RfxEventId} not found");

        var award = new Award
        {
            RfxEventId = rfx.Id,
            ConsolidatedResultRef = body.ConsolidatedResultRef,
            RecommendedSupplierId = body.RecommendedSupplierId,
            Scenario = body.Scenario,
            Status = "pending_approval",
        };

        db.Awards.Add(award);
        await db.SaveChangesAsync(ct);

        return Ok(new AwardResponse(award.Id, award.Status));
    }

    [HttpPost("{id:guid}/final-due-diligence")]
    public async Task<ActionResult<FinalDueDiligenceResponse>> FinalDueDiligence(
        Guid id, [FromBody] FinalDueDiligenceRequest body, CancellationToken ct)
    {
        var award = await db.Awards.FindAsync([id], ct)
            ?? throw new NotFoundException($"Award {id} not found");

        var record = await db.DueDiligenceRecords
            .Where(r => r.SupplierId == award.RecommendedSupplierId)
            .OrderByDescending(r => r.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var ready = record is not null && IsReady(record);

        var exemptionRecorded = !string.IsNullOrWhiteSpace(body.ExemptionReason) && !string.IsNullOrWhiteSpace(body.ExemptionApproverId);

        if (!ready && !exemptionRecorded)
        {
            throw new UnprocessableException(
                "Final due diligence is not ready for the recommended supplier, and no exemptionReason/exemptionApproverId was supplied.");
        }

        return Ok(new FinalDueDiligenceResponse(award.Id, ready, exemptionRecorded));
    }

    /// <summary>Mirrors DueDiligenceController.ComputeDueDiligenceResponse's readiness formula (STORY-M5-2).</summary>
    private static bool IsReady(DueDiligenceRecord record)
    {
        var isSourceable = string.Equals(record.SourcingType, "Sourceable", StringComparison.OrdinalIgnoreCase);
        var isAddressable = string.Equals(record.SpendType, "Addressable Spend", StringComparison.OrdinalIgnoreCase);

        var threePCRequired = isAddressable;
        var esgRequired = isSourceable && isAddressable;

        var is3pcReady = !threePCRequired || string.Equals(record.ThreePCStatus, "Valid", StringComparison.OrdinalIgnoreCase);
        var isEsgReady = !esgRequired || (record.EsgScore.HasValue && record.EsgScore.Value >= 50);
        var isAssociatedPersonReady = string.Equals(record.AssociatedPersonStatus, "Completed", StringComparison.OrdinalIgnoreCase);
        var isMaterial = string.Equals(record.MaterialityStatus, "Material", StringComparison.OrdinalIgnoreCase);
        var isTprmReady = !isMaterial || (
            string.Equals(record.TprmStatus, "Completed", StringComparison.OrdinalIgnoreCase) &&
            record.MaterialSupplierFormAttached &&
            record.MaterialSupplierDocsAttached &&
            record.TprmEvidenceAttached);

        return is3pcReady && isEsgReady && isAssociatedPersonReady && isTprmReady;
    }
}
