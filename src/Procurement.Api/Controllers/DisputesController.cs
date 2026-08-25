using Microsoft.AspNetCore.Mvc;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/disputes")]
public sealed class DisputesController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DisputeResponse>> Create([FromBody] CreateDisputeRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Description))
        {
            throw new UnprocessableException("description is required.");
        }

        var dispute = new Dispute
        {
            ContractId = body.ContractId,
            SupplierId = body.SupplierId,
            PoNumber = body.PoNumber,
            Grn = body.Grn,
            DisputeType = body.DisputeType,
            Description = body.Description.Trim(),
            FinancialImpact = body.FinancialImpact,
            ResolutionStatus = "open",
        };

        db.Disputes.Add(dispute);
        await db.SaveChangesAsync(ct);

        return Ok(new DisputeResponse(dispute.Id, dispute.ResolutionStatus));
    }
}
