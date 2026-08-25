using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

/// <summary>docs/kb/technical_kb.md Module M2 REST API Listing (Budgets).</summary>
[ApiController]
[Route("api/v1/budgets")]
public sealed class BudgetsController(ProcurementDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BudgetSummaryResponse>> Get(
        [FromQuery] int? fiscalYear, [FromQuery] string? costCentre, CancellationToken ct)
    {
        if (fiscalYear is { } fy && (fy < 2000 || fy > 2099))
        {
            throw new UnprocessableException($"Fiscal year '{fy}' must be a 4-digit year between 2000 and 2099.");
        }

        var query = db.Budgets.Include(b => b.Commitments).AsNoTracking().AsQueryable();
        if (fiscalYear.HasValue) query = query.Where(b => b.FiscalYear == fiscalYear.Value);
        if (!string.IsNullOrWhiteSpace(costCentre)) query = query.Where(b => b.CostCentre == costCentre);

        var budgets = await query.ToListAsync(ct);

        var allocated = budgets.Sum(b => b.AllocatedAmount);
        var actualSpend = budgets.Sum(b => b.ActualSpend);
        var reserved = budgets.SelectMany(b => b.Commitments)
            .Where(c => c.Status == CommitmentStatus.Reserved)
            .Sum(c => c.CommittedAmount);
        var available = allocated - (reserved + actualSpend);
        var utilisationPct = allocated > 0 ? ((reserved + actualSpend) / allocated) * 100m : 0m;

        var byCostCentre = budgets.GroupBy(b => b.CostCentre).Select(g =>
        {
            var ccAllocated = g.Sum(b => b.AllocatedAmount);
            var ccSpend = g.Sum(b => b.ActualSpend);
            var ccReserved = g.SelectMany(b => b.Commitments).Where(c => c.Status == CommitmentStatus.Reserved).Sum(c => c.CommittedAmount);
            var capexBalance = g.Where(b => b.CostType == CostType.Capex).Sum(b => b.AllocatedAmount) -
                               (g.Where(b => b.CostType == CostType.Capex).Sum(b => b.ActualSpend) +
                                g.Where(b => b.CostType == CostType.Capex).SelectMany(b => b.Commitments).Where(c => c.Status == CommitmentStatus.Reserved).Sum(c => c.CommittedAmount));
            var opexBalance = g.Where(b => b.CostType == CostType.Opex).Sum(b => b.AllocatedAmount) -
                              (g.Where(b => b.CostType == CostType.Opex).Sum(b => b.ActualSpend) +
                               g.Where(b => b.CostType == CostType.Opex).SelectMany(b => b.Commitments).Where(c => c.Status == CommitmentStatus.Reserved).Sum(c => c.CommittedAmount));
            var util = ccAllocated > 0 ? ((ccReserved + ccSpend) / ccAllocated) * 100m : 0m;
            return new CostCentreBreakdown(g.Key, capexBalance, opexBalance, Math.Round(util, 2));
        }).ToList();

        var byMonth = Enumerable.Range(1, 12).Select(m => new MonthlyBreakdown(m, 0m, 0m, 0m)).ToList();

        return Ok(new BudgetSummaryResponse(
            allocated, reserved, actualSpend, available, Math.Round(utilisationPct, 2), byCostCentre, byMonth));
    }

    [HttpPost("availability-check")]
    public async Task<ActionResult<BudgetAvailabilityCheckResponse>> AvailabilityCheck(
        [FromBody] BudgetAvailabilityCheckRequest body, CancellationToken ct)
    {
        var budget = await db.Budgets.Include(b => b.Commitments).FirstOrDefaultAsync(
            b => b.CostCentre == body.CostCentre && b.GlAccount == body.GlAccount && b.FiscalYear == body.FiscalYear,
            ct);

        if (budget is null)
        {
            return Ok(new BudgetAvailabilityCheckResponse(false, 0m, body.RequestedAmount));
        }

        var reserved = budget.Commitments
            .Where(c => c.Status == CommitmentStatus.Reserved)
            .Sum(c => c.CommittedAmount);
        var available = budget.AllocatedAmount - (reserved + budget.ActualSpend);

        if (available >= body.RequestedAmount)
        {
            return Ok(new BudgetAvailabilityCheckResponse(true, available, 0m));
        }

        var shortfall = body.RequestedAmount - available;
        return Ok(new BudgetAvailabilityCheckResponse(false, available, shortfall));
    }
}
