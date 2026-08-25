using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class FinanceValidationAndP2PReportTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<SavingsRecord> SeedSavingsRecordAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var rfx = new RfxEvent { SourcingStrategyId = Guid.NewGuid(), Status = "opened" };
        db.RfxEvents.Add(rfx);
        var record = new SavingsRecord { RfxEventId = rfx.Id, BaselineValue = 100_000m, AwardedValue = 88_000m };
        db.SavingsRecords.Add(record);
        await db.SaveChangesAsync();
        return record;
    }

    [Fact]
    public async Task FinanceValidation_NonFinanceCaller_Returns403()
    {
        var record = await SeedSavingsRecordAsync();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/savings-records/{record.Id}/finance-validation")
        {
            Content = JsonContent.Create(new FinanceValidationRequest(Validated: true)),
        };
        request.Headers.Add("X-User-Role", "Requestor");

        var resp = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task FinanceValidation_FinanceCaller_Returns200Validated()
    {
        var record = await SeedSavingsRecordAsync();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/savings-records/{record.Id}/finance-validation")
        {
            Content = JsonContent.Create(new FinanceValidationRequest(Validated: true)),
        };
        request.Headers.Add("X-User-Role", "Finance");

        var resp = await client.SendAsync(request);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<FinanceValidationResponse>();

        Assert.NotNull(body);
        Assert.True(body!.FinanceValidated);
    }

    [Fact]
    public async Task P2PReport_NoLinkedChain_Returns404()
    {
        var client = factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/audit-packs/{Guid.NewGuid()}/p2p-report");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
