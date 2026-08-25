using Microsoft.AspNetCore.Mvc;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

/// <summary>
/// docs/kb/technical_kb.md Module M1: GET /api/v1/catalogue/items — catalogue-first check
/// before the non-catalogue intake form (docs/kb/ui_ux.md Module M1, CatalogueSearchPanel).
/// Seed data only; no catalogue-management ticket exists yet in this backlog.
/// </summary>
[ApiController]
[Route("api/v1/catalogue")]
public sealed class CatalogueController : ControllerBase
{
    private static readonly CatalogueItemResponse[] SeedItems =
    [
        new("Laptop Standard Model", "IT and Telecommunication", "Approved Catalogue Supplier"),
        new("Office Stationery Pack", "General Spend", "Approved Catalogue Supplier"),
    ];

    [HttpGet("items")]
    public ActionResult<IReadOnlyList<CatalogueItemResponse>> Search([FromQuery] string? query)
    {
        var items = string.IsNullOrWhiteSpace(query)
            ? SeedItems
            : SeedItems.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();

        return Ok(items);
    }
}
