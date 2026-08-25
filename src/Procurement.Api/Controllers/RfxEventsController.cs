using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/rfx-events")]
public sealed class RfxEventsController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RfxEventResponse>> Create([FromBody] CreateRfxEventRequest body, CancellationToken ct)
    {
        if (body.SourcingStrategyId == Guid.Empty)
        {
            throw new UnprocessableException("SourcingStrategyId is required.");
        }

        var strategy = await db.SourcingStrategies.FindAsync([body.SourcingStrategyId], ct)
            ?? throw new NotFoundException($"Sourcing strategy {body.SourcingStrategyId} not found");

        var rfx = new RfxEvent
        {
            SourcingStrategyId = strategy.Id,
            TechnicalTemplateId = body.TechnicalTemplateId,
            CommercialTemplateId = body.CommercialTemplateId,
            ContractTemplateId = body.ContractTemplateId,
            TenderType = body.TenderType?.ToLowerInvariant() == "invited" ? "invited" : "open",
            Status = "draft",
        };

        db.RfxEvents.Add(rfx);
        await db.SaveChangesAsync(ct);

        return Ok(new RfxEventResponse(
            rfx.Id, rfx.SourcingStrategyId, rfx.TenderType, rfx.Status, rfx.OpeningDateUtc, rfx.ClosingDateUtc, rfx.CreatedAtUtc));
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<PublishRfxResponse>> Publish(
        Guid id, [FromBody] PublishRfxRequest? body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        if (!rfx.TechnicalTemplateId.HasValue || !rfx.CommercialTemplateId.HasValue)
        {
            throw new UnprocessableException(
                "Cannot publish RFx event: technical and commercial template configurations are incomplete.");
        }

        rfx.Status = "published";
        rfx.OpeningDateUtc = body?.OpeningDateUtc ?? DateTime.UtcNow;
        rfx.ClosingDateUtc = body?.ClosingDateUtc ?? DateTime.UtcNow.AddDays(14);

        await db.SaveChangesAsync(ct);

        return Ok(new PublishRfxResponse(
            rfx.Id, "published", rfx.OpeningDateUtc.Value, rfx.ClosingDateUtc.Value));
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<InviteSuppliersResponse>> InviteSuppliers(
        Guid id, [FromBody] InviteSuppliersRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        var suppliers = await db.Suppliers
            .Where(s => body.SupplierIds.Contains(s.Id))
            .ToListAsync(ct);

        var inactiveSupplier = suppliers.FirstOrDefault(s => !s.ActiveFlag);
        if (inactiveSupplier != null)
        {
            throw new UnprocessableException($"Supplier {inactiveSupplier.Id} is inactive and cannot be invited.");
        }

        foreach (var sId in body.SupplierIds)
        {
            db.RfxInvitations.Add(new RfxInvitation
            {
                RfxEventId = rfx.Id,
                SupplierId = sId,
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new InviteSuppliersResponse(body.SupplierIds));
    }

    [HttpPost("{id:guid}/extend")]
    public async Task<ActionResult<ExtendRfxDeadlineResponse>> ExtendDeadline(
        Guid id, [FromBody] ExtendRfxDeadlineRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        if (string.IsNullOrWhiteSpace(body.Reason))
        {
            throw new UnprocessableException("Reason is required to extend the RFx submission deadline.");
        }

        rfx.ClosingDateUtc = body.NewClosingDate;
        rfx.ExtensionReason = body.Reason.Trim();

        await db.SaveChangesAsync(ct);

        return Ok(new ExtendRfxDeadlineResponse(rfx.Id, rfx.ClosingDateUtc.Value));
    }

    [HttpPost("{id:guid}/submissions")]
    public async Task<ActionResult<SubmitProposalResponse>> SubmitProposal(
        Guid id, [FromBody] SubmitProposalRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        if (rfx.ClosingDateUtc.HasValue && DateTime.UtcNow > rfx.ClosingDateUtc.Value && !body.LateExceptionGranted)
        {
            throw new ConflictException(
                "RFx event deadline has passed. Late submissions are blocked without an approved exception.");
        }

        var submission = new RfxSubmission
        {
            RfxEventId = rfx.Id,
            SupplierId = body.SupplierId,
            TechnicalProposal = body.TechnicalProposal,
            CommercialProposal = body.CommercialProposal,
            BidStatus = "submitted",
            LateExceptionGranted = body.LateExceptionGranted,
            SubmittedAtUtc = DateTime.UtcNow,
        };

        db.RfxSubmissions.Add(submission);
        await db.SaveChangesAsync(ct);

        return Ok(new SubmitProposalResponse(submission.Id, submission.SubmittedAtUtc, submission.BidStatus));
    }

    [HttpPost("{id:guid}/open")]
    public async Task<ActionResult<OpenProposalsResponse>> OpenProposals(Guid id, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        if (rfx.ClosingDateUtc.HasValue && DateTime.UtcNow < rfx.ClosingDateUtc.Value)
        {
            throw new ForbiddenException("Cannot open sealed proposals before the RFx closing deadline has passed.");
        }

        rfx.Status = "opened";
        rfx.OpenedBy = "Tender Opening Committee";
        rfx.OpenedAtUtc = DateTime.UtcNow;

        var submissions = await db.RfxSubmissions
            .Where(s => s.RfxEventId == rfx.Id)
            .ToListAsync(ct);

        foreach (var s in submissions)
        {
            s.BidStatus = "opened";
        }

        await db.SaveChangesAsync(ct);

        var submissionDtos = submissions.Select(s =>
            new RfxSubmissionResponse(s.Id, s.SupplierId, s.TechnicalProposal, s.CommercialProposal, s.BidStatus, s.SubmittedAtUtc)).ToList();

        return Ok(new OpenProposalsResponse(rfx.OpenedBy, rfx.OpenedAtUtc.Value, submissionDtos));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RfxEventResponse>> Cancel(
        Guid id, [FromBody] CancelRfxRequest body, CancellationToken ct)
    {
        var rfx = await db.RfxEvents.FindAsync([id], ct)
            ?? throw new NotFoundException($"RFx event {id} not found");

        if (string.IsNullOrWhiteSpace(body.Reason))
        {
            throw new UnprocessableException("Cancellation reason is mandatory.");
        }

        rfx.Status = "cancelled";
        rfx.CancellationReason = body.Reason.Trim();

        await db.SaveChangesAsync(ct);

        return Ok(new RfxEventResponse(
            rfx.Id, rfx.SourcingStrategyId, rfx.TenderType, rfx.Status, rfx.OpeningDateUtc, rfx.ClosingDateUtc, rfx.CreatedAtUtc));
    }
}
