using Microsoft.AspNetCore.Mvc;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SelfBilledController(ProcurementDbContext db) : ControllerBase
{
    /// <summary>
    /// Dev/pilot shared-secret placeholder for the inbound MyInvois webhook signature — no real
    /// signing scheme is named by the source (docs/kb/technical_kb.md Module M11 Open Decisions
    /// already flags external-integration specifics as unconfirmed).
    /// </summary>
    private const string WebhookSharedSecret = "myinvois-webhook-shared-secret";

    [HttpPost("myinvois/webhooks/response")]
    public async Task<ActionResult<MyInvoisWebhookResponse>> ReceiveWebhook(
        [FromHeader(Name = "X-MyInvois-Signature")] string? signature,
        [FromBody] MyInvoisWebhookRequest body, CancellationToken ct)
    {
        if (!string.Equals(signature, WebhookSharedSecret, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var record = await db.SelfBilledRecords.FindAsync([body.SelfBilledRecordId], ct)
            ?? throw new NotFoundException($"Self-billed record {body.SelfBilledRecordId} not found");

        if (string.Equals(body.Status, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            record.Status = "rejected";
            record.RejectionReason = body.RejectionReason;
        }
        else
        {
            record.Status = "accepted";
            record.UniqueId = body.UniqueId;
            record.Qr = body.Qr;
        }

        await db.SaveChangesAsync(ct);

        return Ok(new MyInvoisWebhookResponse(true));
    }

    [HttpPost("self-billed-records/{id:guid}/resubmit")]
    public async Task<ActionResult<ResubmitSelfBilledResponse>> Resubmit(
        Guid id, [FromBody] ResubmitSelfBilledRequest body, CancellationToken ct)
    {
        var record = await db.SelfBilledRecords.FindAsync([id], ct)
            ?? throw new NotFoundException($"Self-billed record {id} not found");

        if (!string.Equals(record.Status, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Self-billed record {id} is not rejected (status: {record.Status}); cannot resubmit.");
        }

        record.Status = "resubmitted";
        record.RejectionReason = null;

        await db.SaveChangesAsync(ct);

        return Ok(new ResubmitSelfBilledResponse(record.Id, record.Status));
    }
}
