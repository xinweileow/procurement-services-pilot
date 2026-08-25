using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Procurement.Api.Common;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class DashboardControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    [Fact]
    public async Task Summary_ReturnsAllFourSections()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("budgetHealth", out _));
        Assert.True(body.TryGetProperty("spendTrend", out _));
        Assert.True(body.TryGetProperty("pendingApprovals", out _));
        Assert.True(body.TryGetProperty("financeConsole", out _));
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var client = factory.CreateClient();
        var draftRequest = new CreateRequestRequest
        {
            RequesterId = "user-3", RequesterRole = "IT Business Requestor", BusinessUnit = "IT",
            CostCentre = "IT-01", Category = "IT and Telecommunication", EstimatedValue = 1000,
            Currency = "MYR", Title = "Draft item", Description = "A draft request.",
            Department = "IT", Country = "Malaysia", Entity = "Etiqa",
            DeliveryDate = new DateOnly(2026, 12, 31), Criticality = "Standard",
            EngagementPathway = "Sourcing Only",
        };
        await client.PostAsJsonAsync("/api/v1/requests", draftRequest);

        var response = await client.GetAsync("/api/v1/requests?status=Draft");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<RequestResponse>>();
        Assert.NotEmpty(page!.Items);
        Assert.All(page.Items, r => Assert.Equal("Draft", r.Status));
    }
}
