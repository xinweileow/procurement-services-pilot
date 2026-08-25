using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;
using Procurement.Api.Services;

namespace Procurement.Api.Controllers;

/// <summary>docs/kb/technical_kb.md Module M1 REST API Listing.</summary>
[ApiController]
[Route("api/v1/requests")]
public sealed class RequestsController(ProcurementDbContext db, IRequestSubmissionService submissionService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RequestResponse>> Create([FromBody] CreateRequestRequest body, CancellationToken ct)
    {
        var request = new Request
        {
            RequestId = $"PR-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(100000, 999999)}",
            RequesterId = body.RequesterId,
            RequesterRole = body.RequesterRole,
            BusinessUnit = body.BusinessUnit,
            CostCentre = body.CostCentre,
            Category = body.Category,
            EstimatedValue = body.EstimatedValue,
            Currency = body.Currency,
            Title = body.Title,
            Description = body.Description,
            Department = body.Department,
            Country = body.Country,
            Entity = body.Entity,
            DeliveryDate = body.DeliveryDate,
            Criticality = body.Criticality,
            EngagementPathway = body.EngagementPathway,
            ProcurementNature = body.ProcurementNature,
            PreviousContractId = body.PreviousContractId,
            IsNonCatalogue = body.IsNonCatalogue,
            Status = RequestStatus.Draft,
        };

        db.Requests.Add(request);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, RequestResponse.From(request));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequestResponse>> GetById(Guid id, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([id], ct)
            ?? throw new NotFoundException($"Request {id} not found");
        return Ok(RequestResponse.From(request));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<RequestResponse>>> List(
        [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = db.Requests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(r => r.Category == category);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => RequestResponse.From(r))
            .ToListAsync(ct);

        return Ok(new PagedResponse<RequestResponse>(items, page, pageSize, total));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<RequestResponse>> Update(Guid id, [FromBody] UpdateRequestRequest body, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([id], ct)
            ?? throw new NotFoundException($"Request {id} not found");
        if (request.Status != RequestStatus.Draft)
        {
            throw new ConflictException($"Request {id} is no longer a draft and cannot be edited.");
        }

        if (body.Title is not null) request.Title = body.Title;
        if (body.Description is not null) request.Description = body.Description;
        if (body.Category is not null) request.Category = body.Category;
        if (body.Department is not null) request.Department = body.Department;
        if (body.EstimatedValue is not null) request.EstimatedValue = body.EstimatedValue.Value;
        if (body.DeliveryDate is not null) request.DeliveryDate = body.DeliveryDate.Value;
        if (body.Criticality is not null) request.Criticality = body.Criticality;

        await db.SaveChangesAsync(ct);
        return Ok(RequestResponse.From(request));
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(RequestAttachment.MaxSizeBytes)]
    public async Task<ActionResult<AttachmentResponse>> AddAttachment(
        Guid id, [FromForm] string documentType, [FromForm] IFormFile file, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([id], ct)
            ?? throw new NotFoundException($"Request {id} not found");

        if (file.Length > RequestAttachment.MaxSizeBytes)
        {
            throw new UnprocessableException($"File exceeds the {RequestAttachment.MaxSizeBytes / (1024 * 1024)} MB limit.");
        }

        var attachment = new RequestAttachment
        {
            RequestId = request.Id,
            DocumentType = documentType,
            FileName = file.FileName,
            SizeBytes = file.Length,
        };
        db.RequestAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        return Ok(AttachmentResponse.From(attachment));
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<RequestResponse>> Submit(Guid id, [FromBody] SubmitRequestRequest body, CancellationToken ct)
    {
        var request = await submissionService.SubmitAsync(id, body, ct);
        return Ok(RequestResponse.From(request));
    }
}
