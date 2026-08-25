using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Services;

public interface IRequestSubmissionService
{
    Task<Request> SubmitAsync(Guid id, SubmitRequestRequest body, CancellationToken ct);
}

/// <summary>
/// docs/kb/technical_kb.md Module M1, STORY-M1-2: validate, run duplicate/anti-splitting
/// detection, set the GSP/EtiqaInternalProcurement route, and lock the request as submitted.
/// </summary>
public sealed class RequestSubmissionService(ProcurementDbContext db) : IRequestSubmissionService
{
    /// <summary>Malaysia RM100,000 and above re-routes to GSP (docs/kb/business_kb.md Module M1/M4).</summary>
    private const decimal MalaysiaGspThreshold = 100_000m;

    /// <summary>How recent a same requester+category+supplier request counts as a possible
    /// duplicate/split (docs/kb/business_kb.md Module M1, S.1.8: "submitted within 45 days" in
    /// the prototype's related-request example).</summary>
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromDays(45);

    public async Task<Request> SubmitAsync(Guid id, SubmitRequestRequest body, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([id], ct)
            ?? throw new NotFoundException($"Request {id} not found");

        if (request.Status != RequestStatus.Draft)
        {
            throw new ConflictException($"Request {id} has already been submitted (status: {request.Status}).");
        }

        if (!(body.NoConflict && body.ConnectedPartyDeclared && body.NoSplittingDeclaration && body.CompleteDeclaration))
        {
            throw new UnprocessableException(
                "All four compliance declarations (no conflict of interest, connected-party, no-splitting, complete-and-accurate) must be true before submission.");
        }

        request.DeclarationNoConflict = body.NoConflict;
        request.DeclarationConnectedPartyDeclared = body.ConnectedPartyDeclared;
        request.DeclarationNoSplitting = body.NoSplittingDeclaration;
        request.DeclarationComplete = body.CompleteDeclaration;

        request.PotentialDuplicateOrSplit = await DetectDuplicateOrSplitAsync(request, ct);

        request.RoutingDestination = request.Country == "Malaysia" && request.EstimatedValue >= MalaysiaGspThreshold
            ? RoutingDestination.Gsp
            : RoutingDestination.EtiqaInternalProcurement;

        request.Status = RequestStatus.Submitted;
        request.SubmittedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return request;
    }

    private async Task<bool> DetectDuplicateOrSplitAsync(Request request, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - DuplicateWindow;
        return await db.Requests.AsNoTracking().AnyAsync(
            r => r.Id != request.Id
                 && r.RequesterId == request.RequesterId
                 && r.Category == request.Category
                 && r.CreatedAtUtc >= cutoff,
            ct);
    }
}
