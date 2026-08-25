using Microsoft.AspNetCore.Mvc;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/contracts")]
public sealed class ContractsController(ProcurementDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ContractResponse>> Create([FromBody] CreateContractRequest body, CancellationToken ct)
    {
        var award = await db.Awards.FindAsync([body.AwardId], ct)
            ?? throw new NotFoundException($"Award {body.AwardId} not found");

        var contract = new Contract
        {
            AwardId = award.Id,
            SupplierId = award.RecommendedSupplierId,
            TemplateId = body.TemplateId,
            Value = body.Value,
            VersionNumber = 1,
            Status = "drafting",
            EffectiveStart = DateTime.UtcNow.Date,
            EffectiveEnd = DateTime.UtcNow.Date.AddYears(1),
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(ct);

        return Ok(new ContractResponse(contract.Id, contract.Status));
    }

    [HttpPost("{id:guid}/sign")]
    public async Task<ActionResult<SignContractResponse>> Sign(Guid id, [FromBody] SignContractRequest body, CancellationToken ct)
    {
        var contract = await db.Contracts.FindAsync([id], ct)
            ?? throw new NotFoundException($"Contract {id} not found");

        var authorityLimit = SignatoryAuthority.LimitFor(body.SignatoryId);
        if (authorityLimit < contract.Value)
        {
            throw new ForbiddenException(
                $"Signatory '{body.SignatoryId}' has an authority limit of {authorityLimit} which is below the contract value of {contract.Value}.");
        }

        contract.Status = "signed";
        contract.ExecutionDate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new SignContractResponse(contract.Id, contract.Status, contract.ExecutionDate.Value));
    }

    [HttpPost("{id:guid}/pricebook-lines")]
    public async Task<ActionResult<PricebookLineResponse>> AddPricebookLine(
        Guid id, [FromBody] CreatePricebookLineRequest body, CancellationToken ct)
    {
        var contract = await db.Contracts.FindAsync([id], ct)
            ?? throw new NotFoundException($"Contract {id} not found");

        if (string.Equals(contract.Status, "expired", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnprocessableException($"Contract {id} is expired; cannot publish a new pricebook line.");
        }

        var line = new PricebookLine
        {
            ContractId = contract.Id,
            ItemDescription = body.ItemDescription,
            Sku = body.Sku,
            UnitPrice = body.UnitPrice,
            Uom = body.Uom,
            Currency = body.Currency,
            ValidFrom = body.ValidFrom,
            ValidTo = body.ValidTo,
        };

        db.PricebookLines.Add(line);
        await db.SaveChangesAsync(ct);

        return Ok(new PricebookLineResponse(line.Id));
    }

    [HttpPost("{id:guid}/agmt")]
    public async Task<ActionResult<IssueAgmtResponse>> IssueAgmt(Guid id, CancellationToken ct)
    {
        var contract = await db.Contracts.FindAsync([id], ct)
            ?? throw new NotFoundException($"Contract {id} not found");

        if (!string.Equals(contract.Status, "signed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Contract {id} is not signed; cannot issue an AGMT ID.");
        }

        contract.AgmtId ??= $"AGMT-{DateTime.UtcNow:yyyy}-{contract.Id.ToString()[..8].ToUpperInvariant()}";

        await db.SaveChangesAsync(ct);

        return Ok(new IssueAgmtResponse(contract.AgmtId));
    }
}
