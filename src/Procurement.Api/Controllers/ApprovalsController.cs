using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/approvals")]
public sealed class ApprovalsController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost("route")]
    public async Task<ActionResult<RouteApprovalResponse>> Route([FromBody] RouteApprovalRequest body, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([body.RequestId], ct)
            ?? throw new NotFoundException($"Request {body.RequestId} not found");

        var gates = new List<ApprovalGateDto>();

        // Banding from prototype/S2P doc:
        // >= 5M -> Board
        // >= 3M -> EXCO
        // >= 1M -> GPPC/EXCO
        // >= 500k -> ETC/GPPC
        // < 500k -> Department + Procurement
        if (request.EstimatedValue >= 5_000_000)
            gates.Add(new ApprovalGateDto("Board", "board-chair"));
        else if (request.EstimatedValue >= 3_000_000)
            gates.Add(new ApprovalGateDto("Exco", "exco-chair"));
        else if (request.EstimatedValue >= 1_000_000)
            gates.Add(new ApprovalGateDto("GppcExco", "gppc-approver"));
        else if (request.EstimatedValue >= 500_000)
            gates.Add(new ApprovalGateDto("EtcGppc", "etc-approver"));
        else
            gates.Add(new ApprovalGateDto("Department", "dept-head"));

        // Persist tasks if none exist yet for this request
        foreach (var g in gates)
        {
            if (Enum.TryParse<ApprovalGate>(g.Gate, true, out var parsedGate))
            {
                var existing = await db.ApprovalTasks.AnyAsync(t => t.RequestId == body.RequestId && t.Gate == parsedGate, ct);
                if (!existing)
                {
                    db.ApprovalTasks.Add(new ApprovalTask
                    {
                        RequestId = body.RequestId,
                        Gate = parsedGate,
                        ApproverId = g.ApproverId,
                        Status = ApprovalTaskStatus.Pending,
                    });
                }
            }
        }
        await db.SaveChangesAsync(ct);

        return Ok(new RouteApprovalResponse(gates));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ApprovalTaskResponse>>> List(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = db.ApprovalTasks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ApprovalTaskStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new ApprovalTaskResponse(
                t.Id, t.RequestId, t.Gate.ToString(), t.ApproverId, t.Status.ToString(), t.DecisionComments, t.DecidedAtUtc))
            .ToListAsync(ct);

        return Ok(new PagedResponse<ApprovalTaskResponse>(items, page, pageSize, total));
    }

    [HttpPost("{taskId:guid}/decision")]
    public async Task<ActionResult<ApprovalTaskResponse>> Decision(
        Guid taskId, [FromBody] ApprovalDecisionRequest body, CancellationToken ct)
    {
        var task = await db.ApprovalTasks.FindAsync([taskId], ct)
            ?? throw new NotFoundException($"Approval task {taskId} not found");

        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new ConflictException($"Approval task {taskId} has already been decided (status: {task.Status}).");
        }

        task.Status = body.Decision.Equals("approve", StringComparison.OrdinalIgnoreCase)
            ? ApprovalTaskStatus.Approved
            : ApprovalTaskStatus.Rejected;
        task.DecisionComments = body.Comments;
        task.DecidedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new ApprovalTaskResponse(
            task.Id, task.RequestId, task.Gate.ToString(), task.ApproverId, task.Status.ToString(), task.DecisionComments, task.DecidedAtUtc));
    }
}
