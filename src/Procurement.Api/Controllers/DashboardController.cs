using Microsoft.AspNetCore.Mvc;

namespace Procurement.Api.Controllers;

/// <summary>
/// docs/kb/technical_kb.md Module M1: GET /api/v1/dashboard/summary. budgetHealth/spendTrend
/// are stubbed pending EPIC-M2's budget API per this ticket's own Scope note — a later ticket
/// wires these to the real Budget entity once EPIC-M2 exists.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult Summary()
    {
        return Ok(new
        {
            budgetHealth = new { allocated = 0m, available = 0m, reserved = 0m, spent = 0m },
            spendTrend = Array.Empty<object>(),
            pendingApprovals = Array.Empty<object>(),
            financeConsole = Array.Empty<object>(),
        });
    }
}
