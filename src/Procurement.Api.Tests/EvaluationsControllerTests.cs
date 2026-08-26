using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class EvaluationsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
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
    public async Task ScoreEvaluation_WhenLocked_Returns409Conflict()
    {
        var rfx = await SeedRfxEventAsync();
        var supplierId = Guid.NewGuid();
        var client = factory.CreateClient();

        // First score and lock
        var lockReq = new ScoreEvaluationRequest(
            SupplierId: supplierId,
            EvaluatorId: "eval-01",
            TechnicalScore: 85,
            CommercialScore: 90,
            Lock: true);

        var firstResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/evaluations", lockReq);
        firstResp.EnsureSuccessStatusCode();

        // Attempt to update locked evaluation
        var updateReq = new ScoreEvaluationRequest(
            SupplierId: supplierId,
            EvaluatorId: "eval-01",
            TechnicalScore: 95,
            CommercialScore: 95);

        var secondResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/evaluations", updateReq);
        Assert.Equal(HttpStatusCode.Conflict, secondResp.StatusCode);
    }

    [Fact]
    public async Task ListEvaluations_ReturnsScoredEvaluationsForEvent()
    {
        var rfx = await SeedRfxEventAsync();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/evaluations", new ScoreEvaluationRequest(
            SupplierId: Guid.NewGuid(), EvaluatorId: "eval-01", TechnicalScore: 80, CommercialScore: 70));

        var response = await client.GetAsync($"/api/v1/rfx-events/{rfx.Id}/evaluations");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<List<EvaluationResponse>>();

        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal(80, body[0].TechnicalScore);
    }

    [Fact]
    public async Task ListClarifications_ReturnsClarificationsForEvent()
    {
        var rfx = await SeedRfxEventAsync();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/clarifications", new RaiseClarificationRequest(
            SupplierId: Guid.NewGuid(), Category: "Commercial", Question: "Clarify pricing basis"));

        var response = await client.GetAsync($"/api/v1/rfx-events/{rfx.Id}/clarifications");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<List<ClarificationResponse>>();

        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal("pending", body[0].Status);
    }

    [Fact]
    public async Task StatusGateDecision_WithUnresolvedMaterialDeviation_Returns422()
    {
        var rfx = await SeedRfxEventAsync();
        var client = factory.CreateClient();

        // Raise a material deviation
        var clarResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/clarifications", new RaiseClarificationRequest(
            SupplierId: Guid.NewGuid(),
            Category: "Technical",
            Question: "Missing architecture diagram",
            IsMaterialDeviation: true));
        clarResp.EnsureSuccessStatusCode();

        // Try to pass status gate
        var gateResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/status-gate-decision", new StatusGateDecisionRequest(
            Decision: "proceed"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, gateResp.StatusCode);
    }

    [Fact]
    public async Task StatusGateDecision_WhenAllMaterialDeviationsResolved_Returns200()
    {
        var rfx = await SeedRfxEventAsync();
        var client = factory.CreateClient();

        // Raise a material deviation
        var clarResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/clarifications", new RaiseClarificationRequest(
            SupplierId: Guid.NewGuid(),
            Category: "Technical",
            Question: "Missing architecture diagram",
            IsMaterialDeviation: true));
        var clar = await clarResp.Content.ReadFromJsonAsync<ClarificationResponse>();

        // Resolve the deviation
        var resolveResp = await client.PatchAsJsonAsync($"/api/v1/clarifications/{clar!.Id}", new ResolveClarificationRequest(
            Response: "Architecture diagram provided in Appendix B"));
        resolveResp.EnsureSuccessStatusCode();

        // Now pass status gate
        var gateResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/status-gate-decision", new StatusGateDecisionRequest(
            Decision: "proceed"));

        gateResp.EnsureSuccessStatusCode();
        var gateBody = await gateResp.Content.ReadFromJsonAsync<StatusGateDecisionResponse>();
        Assert.NotNull(gateBody);
        Assert.True(gateBody.Passed);
    }
}
