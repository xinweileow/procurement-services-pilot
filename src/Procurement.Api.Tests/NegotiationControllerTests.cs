using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class NegotiationControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<RfxEvent> SeedRfxEventAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var rfx = new RfxEvent
        {
            SourcingStrategyId = Guid.NewGuid(),
            Status = "opened",
        };
        db.RfxEvents.Add(rfx);
        await db.SaveChangesAsync();
        return rfx;
    }

    [Fact]
    public async Task RecordNegotiationRound_TwoConsecutiveCallsForSameSupplier_ProducesIncrementingRoundNumbers()
    {
        var rfx = await SeedRfxEventAsync();
        var supplierId = Guid.NewGuid();
        var client = factory.CreateClient();

        var firstResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/negotiation-rounds", new RecordNegotiationRoundRequest(
            SupplierId: supplierId,
            RevisedPrice: 100000m,
            Terms: "Original offer",
            RoundType: "negotiation"));
        firstResp.EnsureSuccessStatusCode();
        var first = await firstResp.Content.ReadFromJsonAsync<NegotiationRoundResponse>();

        var secondResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/negotiation-rounds", new RecordNegotiationRoundRequest(
            SupplierId: supplierId,
            RevisedPrice: 95000m,
            Terms: "Revised offer",
            RoundType: "negotiation"));
        secondResp.EnsureSuccessStatusCode();
        var second = await secondResp.Content.ReadFromJsonAsync<NegotiationRoundResponse>();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, first!.RoundNumber);
        Assert.Equal(2, second!.RoundNumber);
        Assert.NotEqual(first.Id, second.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var rounds = db.NegotiationRounds.Where(r => r.RfxEventId == rfx.Id && r.SupplierId == supplierId).ToList();
        Assert.Equal(2, rounds.Count);
        Assert.Contains(rounds, r => r.Id == first.Id && r.RevisedPrice == 100000m);
        Assert.Contains(rounds, r => r.Id == second.Id && r.RevisedPrice == 95000m);
    }

    [Fact]
    public async Task RecordNegotiationRound_WhenRfxEventDoesNotExist_Returns404()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{Guid.NewGuid()}/negotiation-rounds", new RecordNegotiationRoundRequest(
            SupplierId: Guid.NewGuid(),
            RevisedPrice: 50000m,
            Terms: null,
            RoundType: "negotiation"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetConsolidatedResult_WhenSupplierEvaluationIsNotLocked_Returns409Conflict()
    {
        var rfx = await SeedRfxEventAsync();
        var supplierId = Guid.NewGuid();
        var client = factory.CreateClient();

        var scoreResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/evaluations", new ScoreEvaluationRequest(
            SupplierId: supplierId,
            EvaluatorId: "eval-01",
            TechnicalScore: 80,
            CommercialScore: 85,
            Lock: false));
        scoreResp.EnsureSuccessStatusCode();

        var resultResp = await client.GetAsync($"/api/v1/rfx-events/{rfx.Id}/consolidated-result?supplierId={supplierId}");

        Assert.Equal(HttpStatusCode.Conflict, resultResp.StatusCode);
    }

    [Fact]
    public async Task CalculateSavings_WhenBaselineMissing_Returns422()
    {
        var rfx = await SeedRfxEventAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/savings-calculation", new SavingsCalculationRequest(
            BaselineValue: null,
            AwardedValue: 90000m,
            Method: "hard"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task CalculateSavings_WithBaselineAndAwardedValue_ReturnsSavingsAmountWithFinanceValidatedFalse()
    {
        var rfx = await SeedRfxEventAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/savings-calculation", new SavingsCalculationRequest(
            BaselineValue: 100000m,
            AwardedValue: 88000m,
            Method: "hard"));

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<SavingsCalculationResponse>();

        Assert.NotNull(body);
        Assert.Equal(12000m, body!.SavingsAmount);
        Assert.Equal("hard", body.SavingsType);
        Assert.False(body.FinanceValidated);
    }
}
