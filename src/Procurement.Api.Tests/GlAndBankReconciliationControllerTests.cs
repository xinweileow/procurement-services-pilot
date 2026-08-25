using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class GlAndBankReconciliationControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    [Fact]
    public async Task CreateGlEntry_DebitCreditImbalance_Returns422()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/gl-entries", new CreateGlEntryRequest(
            GlAccount: "6100-000", CostCentre: "FIN-2040", Entity: "Etiqa",
            Debit: 1000m, Credit: 950m, Tax: null, Wht: null, PostingDate: DateTime.UtcNow.Date));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task CreateGlEntry_Balanced_Returns200()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/gl-entries", new CreateGlEntryRequest(
            GlAccount: "6100-000", CostCentre: "FIN-2040", Entity: "Etiqa",
            Debit: 1000m, Credit: 1000m, Tax: null, Wht: null, PostingDate: DateTime.UtcNow.Date));

        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MatchBankRecord_NoPaymentReference_ReturnsUnmatchedAndCreatesException()
    {
        var client = factory.CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/v1/bank-records", new CreateBankRecordRequest(
            BankReference: "BNK-001", Date: DateTime.UtcNow.Date, Amount: 500m, Currency: "MYR"));
        createResp.EnsureSuccessStatusCode();
        var record = await createResp.Content.ReadFromJsonAsync<BankRecordResponse>();

        var matchResp = await client.PostAsJsonAsync($"/api/v1/bank-records/{record!.Id}/match", new MatchBankRecordRequest(
            PaymentId: null));

        Assert.Equal(HttpStatusCode.OK, matchResp.StatusCode);
        var body = await matchResp.Content.ReadFromJsonAsync<MatchBankRecordResponse>();
        Assert.NotNull(body);
        Assert.Equal("unmatched", body!.MatchStatus);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        Assert.Contains(db.ReconciliationExceptions, e => e.BankRecordId == record.Id);
    }
}
