using Microsoft.AspNetCore.Mvc;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/audit-packs")]
public sealed class AuditPacksController(ProcurementDbContext db) : ControllerBase
{
    private const int RetentionYears = 15;

    [HttpPost]
    public async Task<ActionResult<AuditPackResponse>> Create([FromBody] CreateAuditPackRequest body, CancellationToken ct)
    {
        var request = await db.Requests.FindAsync([body.RequestId], ct)
            ?? throw new NotFoundException($"Request {body.RequestId} not found");

        var auditPack = new AuditPack
        {
            RequestId = request.Id,
            RetentionDate = DateTime.UtcNow.Date.AddYears(RetentionYears),
        };

        db.AuditPacks.Add(auditPack);
        await db.SaveChangesAsync(ct);

        return Ok(new AuditPackResponse(auditPack.Id, auditPack.RetentionDate));
    }
}
