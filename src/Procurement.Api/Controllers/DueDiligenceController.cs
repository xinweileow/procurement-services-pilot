using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/requests/{requestId:guid}")]
public sealed class DueDiligenceController(ProcurementDbContext db) : ControllerBase
{
    [HttpGet("due-diligence")]
    public async Task<ActionResult<DueDiligenceResponse>> GetDueDiligence(Guid requestId, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([requestId], ct)
            ?? throw new NotFoundException($"Request {requestId} not found");

        var record = await db.DueDiligenceRecords.FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        if (record is null)
        {
            record = new DueDiligenceRecord
            {
                RequestId = requestId,
                SupplierId = Guid.NewGuid(),
                SourcingType = "Sourceable",
                SpendType = "Addressable Spend",
                ThreePCStatus = "Valid",
                EsgScore = 75,
                AssociatedPersonStatus = "Completed",
                TprmStatus = "Completed",
                MaterialityStatus = "Non-Material",
            };
            db.DueDiligenceRecords.Add(record);
            await db.SaveChangesAsync(ct);
        }

        return Ok(ComputeDueDiligenceResponse(record));
    }

    [HttpPatch("due-diligence-applicability")]
    public async Task<ActionResult<DueDiligenceResponse>> UpdateApplicability(
        Guid requestId, [FromBody] UpdateDueDiligenceApplicabilityRequest body, CancellationToken ct)
    {
        var validSourcingTypes = new[] { "Sourceable", "Non-Sourceable" };
        var validSpendTypes = new[] { "Addressable Spend", "Non-Addressable Spend" };

        if (!validSourcingTypes.Contains(body.SourcingType, StringComparer.OrdinalIgnoreCase) ||
            !validSpendTypes.Contains(body.SpendType, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnprocessableException("Invalid sourcingType or spendType enum value.");
        }

        var record = await db.DueDiligenceRecords.FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        if (record is null)
        {
            record = new DueDiligenceRecord
            {
                RequestId = requestId,
                SupplierId = Guid.NewGuid(),
            };
            db.DueDiligenceRecords.Add(record);
        }

        record.SourcingType = body.SourcingType;
        record.SpendType = body.SpendType;
        if (!string.IsNullOrWhiteSpace(body.MaterialityStatus))
        {
            record.MaterialityStatus = body.MaterialityStatus;
        }
        record.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(ComputeDueDiligenceResponse(record));
    }

    [HttpPost("supplier-exception")]
    public async Task<ActionResult<SupplierExceptionResponse>> CreateSupplierException(
        Guid requestId, [FromBody] CreateSupplierExceptionRequest body, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([requestId], ct)
            ?? throw new NotFoundException($"Request {requestId} not found");

        if (string.IsNullOrWhiteSpace(body.Reason) || !body.ProcurementHeadApprovalAttachmentId.HasValue || body.ProcurementHeadApprovalAttachmentId == Guid.Empty)
        {
            throw new UnprocessableException("Reason and Procurement Head approval attachment are mandatory for a supplier exception.");
        }

        var exception = new SupplierException
        {
            RequestId = requestId,
            Reason = body.Reason.Trim(),
            ProcurementHeadApprovalAttachmentId = body.ProcurementHeadApprovalAttachmentId.Value,
            Status = "pending_approval",
        };

        db.SupplierExceptions.Add(exception);
        await db.SaveChangesAsync(ct);

        return Ok(new SupplierExceptionResponse(
            exception.Id, exception.RequestId, exception.Reason, exception.ProcurementHeadApprovalAttachmentId, exception.Status));
    }

    private static DueDiligenceResponse ComputeDueDiligenceResponse(DueDiligenceRecord record)
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

        var ready = is3pcReady && isEsgReady && isAssociatedPersonReady && isTprmReady;

        return new DueDiligenceResponse(
            record.Id,
            record.RequestId,
            record.SourcingType,
            record.SpendType,
            threePCRequired,
            esgRequired,
            record.ThreePCStatus,
            record.EsgScore,
            record.AssociatedPersonStatus,
            record.TprmStatus,
            record.MaterialityStatus,
            ready);
    }
}
