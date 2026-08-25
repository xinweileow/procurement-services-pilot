using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/budget-exceptions")]
public sealed class BudgetExceptionsController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BudgetExceptionResponse>> Create(
        [FromBody] CreateBudgetExceptionRequest body, CancellationToken ct)
    {
        if (body.Shortfall <= 0)
        {
            throw new UnprocessableException("Shortfall must be greater than zero to create a budget exception.");
        }
        if (string.IsNullOrWhiteSpace(body.Reason))
        {
            throw new UnprocessableException("A justification reason is required for budget exceptions.");
        }

        var exception = new BudgetException
        {
            RequestId = body.RequestId,
            Shortfall = body.Shortfall,
            Reason = body.Reason,
            Status = BudgetExceptionStatus.PendingApproval,
        };

        db.BudgetExceptions.Add(exception);
        await db.SaveChangesAsync(ct);

        return Ok(new BudgetExceptionResponse(exception.Id, exception.RequestId, exception.Shortfall, exception.Status.ToString()));
    }
}
