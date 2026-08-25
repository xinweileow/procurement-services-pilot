using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class ContractsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Award> SeedAwardAsync(decimal contractValue = 0m)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var rfx = new RfxEvent { SourcingStrategyId = Guid.NewGuid(), Status = "opened" };
        db.RfxEvents.Add(rfx);
        var award = new Award { RfxEventId = rfx.Id, RecommendedSupplierId = Guid.NewGuid(), Status = "pending_approval" };
        db.Awards.Add(award);
        await db.SaveChangesAsync();
        return award;
    }

    [Fact]
    public async Task CreateContract_AwardNotFound_Returns404()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/contracts", new CreateContractRequest(
            AwardId: Guid.NewGuid(), TemplateId: null, Value: 100_000m));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task CreateContract_FirstCreation_ReturnsDraftingVersionOne()
    {
        var award = await SeedAwardAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/contracts", new CreateContractRequest(
            AwardId: award.Id, TemplateId: Guid.NewGuid(), Value: 250_000m));

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ContractResponse>();
        Assert.NotNull(body);
        Assert.Equal("drafting", body!.Status);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var contract = db.Contracts.Single(c => c.Id == body.Id);
        Assert.Equal(1, contract.VersionNumber);
    }

    [Fact]
    public async Task SignContract_SignatoryLimitBelowContractValue_Returns403()
    {
        var award = await SeedAwardAsync();
        var client = factory.CreateClient();

        var contractResp = await client.PostAsJsonAsync("/api/v1/contracts", new CreateContractRequest(
            AwardId: award.Id, TemplateId: null, Value: 2_000_000m));
        var contract = await contractResp.Content.ReadFromJsonAsync<ContractResponse>();

        var signResp = await client.PostAsJsonAsync($"/api/v1/contracts/{contract!.Id}/sign", new SignContractRequest(
            SignatoryId: "department"));

        Assert.Equal(HttpStatusCode.Forbidden, signResp.StatusCode);
    }

    [Fact]
    public async Task IssueAgmt_ContractNotSigned_Returns409()
    {
        var award = await SeedAwardAsync();
        var client = factory.CreateClient();

        var contractResp = await client.PostAsJsonAsync("/api/v1/contracts", new CreateContractRequest(
            AwardId: award.Id, TemplateId: null, Value: 10_000m));
        var contract = await contractResp.Content.ReadFromJsonAsync<ContractResponse>();

        var agmtResp = await client.PostAsync($"/api/v1/contracts/{contract!.Id}/agmt", null);

        Assert.Equal(HttpStatusCode.Conflict, agmtResp.StatusCode);
    }

    [Fact]
    public async Task AddPricebookLine_ContractExpired_Returns422()
    {
        var award = await SeedAwardAsync();
        var client = factory.CreateClient();

        var contractResp = await client.PostAsJsonAsync("/api/v1/contracts", new CreateContractRequest(
            AwardId: award.Id, TemplateId: null, Value: 10_000m));
        var contract = await contractResp.Content.ReadFromJsonAsync<ContractResponse>();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var entity = db.Contracts.Single(c => c.Id == contract!.Id);
            entity.Status = "expired";
            await db.SaveChangesAsync();
        }

        var lineResp = await client.PostAsJsonAsync($"/api/v1/contracts/{contract!.Id}/pricebook-lines", new CreatePricebookLineRequest(
            ItemDescription: "Widget", Sku: "SKU-1", UnitPrice: 10m, Uom: "EA", Currency: "MYR",
            ValidFrom: DateTime.UtcNow.Date, ValidTo: DateTime.UtcNow.Date.AddYears(1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, lineResp.StatusCode);
    }
}
