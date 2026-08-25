using Microsoft.AspNetCore.Mvc;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/contracts/{id:guid}/obligations")]
public sealed class ContractGovernanceController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ContractObligationResponse>> Create(
        Guid id, [FromBody] CreateContractObligationRequest body, CancellationToken ct)
    {
        var contract = await db.Contracts.FindAsync([id], ct)
            ?? throw new NotFoundException($"Contract {id} not found");

        if (body.DueDate.Date < DateTime.UtcNow.Date)
        {
            throw new UnprocessableException($"dueDate {body.DueDate:yyyy-MM-dd} is in the past.");
        }

        var obligation = new ContractObligation
        {
            ContractId = contract.Id,
            Description = body.Description,
            Owner = body.Owner,
            DueDate = body.DueDate,
            Status = "open",
        };

        db.ContractObligations.Add(obligation);
        await db.SaveChangesAsync(ct);

        return Ok(new ContractObligationResponse(obligation.Id, obligation.Status));
    }
}
