using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SourcingController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost("sourcing/route-recommendation")]
    public ActionResult<RouteRecommendationResponse> RecommendRoute([FromBody] RouteRecommendationRequest body)
    {
        if (body.EstimatedValue <= 0)
        {
            throw new UnprocessableException("EstimatedValue must be greater than zero.");
        }

        var country = body.Country?.Trim() ?? "Malaysia";
        decimal threshold = country.ToLowerInvariant() switch
        {
            "malaysia" => 100_000m,
            "singapore" => 10_000m,
            "thailand" => 10_000m,
            _ => 5_000m // Cambodia, Indonesia, and Other Countries
        };

        if (body.SingleSource)
        {
            return Ok(new RouteRecommendationResponse(
                RecommendedRoute: "SingleSource",
                MinQuotations: 1,
                RequiresTender: false,
                RequiresGspReroute: false));
        }

        if (body.Emergency)
        {
            return Ok(new RouteRecommendationResponse(
                RecommendedRoute: "SingleSource",
                MinQuotations: 1,
                RequiresTender: false,
                RequiresGspReroute: false));
        }

        var isAtOrAboveThreshold = body.EstimatedValue >= threshold;
        var minQuotations = isAtOrAboveThreshold ? 3 : 1;
        var requiresTender = isAtOrAboveThreshold;
        var requiresGspReroute = country.Equals("Malaysia", StringComparison.OrdinalIgnoreCase) && body.EstimatedValue >= 100_000m;

        return Ok(new RouteRecommendationResponse(
            RecommendedRoute: "RFQ",
            MinQuotations: minQuotations,
            RequiresTender: requiresTender,
            RequiresGspReroute: requiresGspReroute));
    }

    [HttpPost("suppliers/{id:guid}/three-point-check")]
    public async Task<ActionResult<ThreePointCheckResponse>> ThreePointCheck(Guid id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FindAsync([id], ct)
            ?? throw new NotFoundException($"Supplier {id} not found");

        var check = new ThreePointCheck
        {
            SupplierId = supplier.Id,
            IdentityVerified = supplier.ActiveFlag,
            RegistrationEvidenceVerified = string.Equals(supplier.KycStatus, "completed", StringComparison.OrdinalIgnoreCase),
            ActiveStatus = supplier.ActiveFlag,
            CheckedAtUtc = DateTime.UtcNow,
        };

        db.ThreePointChecks.Add(check);
        await db.SaveChangesAsync(ct);

        return Ok(new ThreePointCheckResponse(
            check.Id, check.SupplierId, check.IdentityVerified, check.RegistrationEvidenceVerified, check.ActiveStatus, check.CheckedAtUtc));
    }

    [HttpPost("sourcing/strategies/{id:guid}/agree")]
    public async Task<ActionResult<SourcingStrategyResponse>> AgreeStrategy(
        Guid id, [FromBody] AgreeSourcingStrategyRequest? body, CancellationToken ct)
    {
        var strategy = await db.SourcingStrategies.FindAsync([id], ct)
            ?? throw new NotFoundException($"Sourcing strategy {id} not found");

        if (string.Equals(strategy.Status, "agreed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Sourcing strategy {id} is already agreed.");
        }

        strategy.Status = "agreed";
        await db.SaveChangesAsync(ct);

        return Ok(SourcingStrategyResponse.From(strategy));
    }

    [HttpGet("sourcing/spend-analysis")]
    public ActionResult<SpendAnalysisResponse> SpendAnalysis([FromQuery] string? category)
    {
        var cat = category?.Trim() ?? "General Spend";
        var isTailSpend = cat.Contains("tail", StringComparison.OrdinalIgnoreCase)
            || cat.Contains("stationery", StringComparison.OrdinalIgnoreCase)
            || cat.Contains("general", StringComparison.OrdinalIgnoreCase);

        var historicalSpend = isTailSpend ? 45_000m : 320_000m;
        var benchmarkPrice = isTailSpend ? 42_000m : 300_000m;
        var priceVariancePct = Math.Round(((historicalSpend - benchmarkPrice) / benchmarkPrice) * 100m, 2);

        return Ok(new SpendAnalysisResponse(historicalSpend, benchmarkPrice, priceVariancePct, isTailSpend));
    }
}
