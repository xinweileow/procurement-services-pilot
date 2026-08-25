using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class ContractGovernanceControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Contract> SeedContractAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var rfx = new RfxEvent { SourcingStrategyId = Guid.NewGuid(), Status = "opened" };
        db.RfxEvents.Add(rfx);
        var award = new Award { RfxEventId = rfx.Id, RecommendedSupplierId = Guid.NewGuid(), Status = "approved" };
        db.Awards.Add(award);
        var contract = new Contract { AwardId = award.Id, SupplierId = award.RecommendedSupplierId, Status = "active" };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return contract;
    }

    [Fact]
    public async Task CreateObligation_DueDateInPast_Returns422()
    {
        var contract = await SeedContractAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/contracts/{contract.Id}/obligations", new CreateContractObligationRequest(
            Description: "Renew SLA documentation", Owner: "vendor-manager", DueDate: DateTime.UtcNow.Date.AddDays(-1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task CreateObligation_FutureDueDate_Returns200Open()
    {
        var contract = await SeedContractAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/contracts/{contract.Id}/obligations", new CreateContractObligationRequest(
            Description: "Renew SLA documentation", Owner: "vendor-manager", DueDate: DateTime.UtcNow.Date.AddDays(30)));

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ContractObligationResponse>();
        Assert.NotNull(body);
        Assert.Equal("open", body!.Status);
    }

    [Fact]
    public async Task CreateDispute_MissingDescription_Returns422()
    {
        var contract = await SeedContractAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/disputes", new CreateDisputeRequest(
            ContractId: contract.Id, SupplierId: contract.SupplierId, DisputeType: "Commercial", Description: ""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }
}
