using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class AwardsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<RfxEvent> SeedRfxEventAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var rfx = new RfxEvent { SourcingStrategyId = Guid.NewGuid(), Status = "opened" };
        db.RfxEvents.Add(rfx);
        await db.SaveChangesAsync();
        return rfx;
    }

    [Fact]
    public async Task CreateAward_MissingRecommendedSupplierId_Returns422()
    {
        var rfx = await SeedRfxEventAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/awards", new CreateAwardRequest(
            RfxEventId: rfx.Id,
            ConsolidatedResultRef: "consolidated-1",
            RecommendedSupplierId: Guid.Empty,
            Scenario: "single_award"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task FinalDueDiligence_NotReadyWithoutExemption_Returns422()
    {
        var rfx = await SeedRfxEventAsync();
        var supplierId = Guid.NewGuid();
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.DueDiligenceRecords.Add(new DueDiligenceRecord
            {
                RequestId = Guid.NewGuid(),
                SupplierId = supplierId,
                MaterialityStatus = "Material",
                TprmStatus = "Pending",
                MaterialSupplierDocsAttached = false,
            });
            await db.SaveChangesAsync();
        }

        var awardResp = await client.PostAsJsonAsync("/api/v1/awards", new CreateAwardRequest(
            RfxEventId: rfx.Id, ConsolidatedResultRef: null, RecommendedSupplierId: supplierId, Scenario: "single_award"));
        var award = await awardResp.Content.ReadFromJsonAsync<AwardResponse>();

        var ddResp = await client.PostAsJsonAsync($"/api/v1/awards/{award!.Id}/final-due-diligence", new FinalDueDiligenceRequest(
            ExemptionReason: null, ExemptionApproverId: null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ddResp.StatusCode);
    }

    [Fact]
    public async Task FinalDueDiligence_NotReadyWithExemption_Returns200WithExemptionRecorded()
    {
        var rfx = await SeedRfxEventAsync();
        var supplierId = Guid.NewGuid();
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.DueDiligenceRecords.Add(new DueDiligenceRecord
            {
                RequestId = Guid.NewGuid(),
                SupplierId = supplierId,
                MaterialityStatus = "Material",
                TprmStatus = "Pending",
                MaterialSupplierDocsAttached = false,
            });
            await db.SaveChangesAsync();
        }

        var awardResp = await client.PostAsJsonAsync("/api/v1/awards", new CreateAwardRequest(
            RfxEventId: rfx.Id, ConsolidatedResultRef: null, RecommendedSupplierId: supplierId, Scenario: "single_award"));
        var award = await awardResp.Content.ReadFromJsonAsync<AwardResponse>();

        var ddResp = await client.PostAsJsonAsync($"/api/v1/awards/{award!.Id}/final-due-diligence", new FinalDueDiligenceRequest(
            ExemptionReason: "Supplier is sole proprietary source", ExemptionApproverId: "approver-01"));

        ddResp.EnsureSuccessStatusCode();
        var body = await ddResp.Content.ReadFromJsonAsync<FinalDueDiligenceResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Ready);
        Assert.True(body.ExemptionRecorded);
    }
}
