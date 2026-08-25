using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class SourcingControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Supplier> SeedSupplierAsync(string name = "Tech Supplies Sdn Bhd", bool active = true, string kyc = "completed")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var supplier = new Supplier
        {
            LegalEntityName = name,
            RegistrationNumber = $"REG-{Random.Shared.Next(10000, 99999)}",
            TaxId = $"TAX-{Random.Shared.Next(10000, 99999)}",
            Address = "123 Business Street, Kuala Lumpur",
            ActiveFlag = active,
            KycStatus = kyc,
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    private async Task<SourcingStrategy> SeedStrategyAsync(Guid requestId, string status = "draft")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var strategy = new SourcingStrategy
        {
            RequestId = requestId,
            RecommendedRoute = "RFQ",
            Status = status,
        };
        db.SourcingStrategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy;
    }

    [Fact]
    public async Task RouteRecommendation_MalaysiaUnder100k_ReturnsMin1Quotation()
    {
        var client = factory.CreateClient();
        var request = new RouteRecommendationRequest(
            RequestId: Guid.NewGuid(),
            EstimatedValue: 50_000,
            Country: "Malaysia");

        var response = await client.PostAsJsonAsync("/api/v1/sourcing/route-recommendation", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteRecommendationResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body.MinQuotations);
        Assert.False(body.RequiresTender);
        Assert.False(body.RequiresGspReroute);
    }

    [Fact]
    public async Task RouteRecommendation_MalaysiaAtOrAbove100k_ReturnsMin3QuotationsAndTender()
    {
        var client = factory.CreateClient();
        var request = new RouteRecommendationRequest(
            RequestId: Guid.NewGuid(),
            EstimatedValue: 100_000,
            Country: "Malaysia");

        var response = await client.PostAsJsonAsync("/api/v1/sourcing/route-recommendation", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteRecommendationResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body.MinQuotations);
        Assert.True(body.RequiresTender);
        Assert.True(body.RequiresGspReroute);
    }

    [Theory]
    [InlineData("Singapore", 9_999, 1, false)]
    [InlineData("Singapore", 10_000, 3, true)]
    [InlineData("Thailand", 9_999, 1, false)]
    [InlineData("Thailand", 10_000, 3, true)]
    [InlineData("Indonesia", 4_999, 1, false)]
    [InlineData("Indonesia", 5_000, 3, true)]
    [InlineData("Cambodia", 4_999, 1, false)]
    [InlineData("Cambodia", 5_000, 3, true)]
    public async Task RouteRecommendation_CountryThresholds_ReturnsExpectedMinQuotations(
        string country, decimal value, int expectedMinQuotations, bool expectedTender)
    {
        var client = factory.CreateClient();
        var request = new RouteRecommendationRequest(
            RequestId: Guid.NewGuid(),
            EstimatedValue: value,
            Country: country);

        var response = await client.PostAsJsonAsync("/api/v1/sourcing/route-recommendation", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteRecommendationResponse>();
        Assert.NotNull(body);
        Assert.Equal(expectedMinQuotations, body.MinQuotations);
        Assert.Equal(expectedTender, body.RequiresTender);
    }

    [Fact]
    public async Task RouteRecommendation_SingleSourceTrue_ReturnsSingleSourceRouteRegardlessOfValue()
    {
        var client = factory.CreateClient();
        var request = new RouteRecommendationRequest(
            RequestId: Guid.NewGuid(),
            EstimatedValue: 500_000,
            Country: "Malaysia",
            SingleSource: true);

        var response = await client.PostAsJsonAsync("/api/v1/sourcing/route-recommendation", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteRecommendationResponse>();
        Assert.NotNull(body);
        Assert.Equal("SingleSource", body.RecommendedRoute);
        Assert.Equal(1, body.MinQuotations);
        Assert.False(body.RequiresTender);
    }

    [Fact]
    public async Task RouteRecommendation_EmergencyTrue_ReturnsSingleSourceRoute()
    {
        var client = factory.CreateClient();
        var request = new RouteRecommendationRequest(
            RequestId: Guid.NewGuid(),
            EstimatedValue: 300_000,
            Country: "Malaysia",
            Emergency: true);

        var response = await client.PostAsJsonAsync("/api/v1/sourcing/route-recommendation", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteRecommendationResponse>();
        Assert.NotNull(body);
        Assert.Equal("SingleSource", body.RecommendedRoute);
        Assert.Equal(1, body.MinQuotations);
    }

    [Fact]
    public async Task RouteRecommendation_NonPositiveValue_Returns422()
    {
        var client = factory.CreateClient();
        var request = new RouteRecommendationRequest(
            RequestId: Guid.NewGuid(),
            EstimatedValue: 0,
            Country: "Malaysia");

        var response = await client.PostAsJsonAsync("/api/v1/sourcing/route-recommendation", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ThreePointCheck_NonExistentSupplier_Returns404()
    {
        var client = factory.CreateClient();
        var nonExistentId = Guid.NewGuid();

        var response = await client.PostAsync($"/api/v1/suppliers/{nonExistentId}/three-point-check", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ThreePointCheck_ExistingSupplier_ReturnsVerificationResults()
    {
        var supplier = await SeedSupplierAsync(active: true, kyc: "completed");
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/v1/suppliers/{supplier.Id}/three-point-check", null);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ThreePointCheckResponse>();
        Assert.NotNull(body);
        Assert.Equal(supplier.Id, body.SupplierId);
        Assert.True(body.IdentityVerified);
        Assert.True(body.RegistrationEvidenceVerified);
        Assert.True(body.ActiveStatus);
    }

    [Fact]
    public async Task AgreeStrategy_AgreedSuccessfully_AndReturns409WhenAlreadyAgreed()
    {
        var strategy = await SeedStrategyAsync(Guid.NewGuid(), status: "draft");
        var client = factory.CreateClient();

        // First call: succeeds and sets status to agreed
        var first = await client.PostAsJsonAsync($"/api/v1/sourcing/strategies/{strategy.Id}/agree", new AgreeSourcingStrategyRequest());
        first.EnsureSuccessStatusCode();
        var agreedBody = await first.Content.ReadFromJsonAsync<SourcingStrategyResponse>();
        Assert.NotNull(agreedBody);
        Assert.Equal("agreed", agreedBody.Status);

        // Second call: returns 409 Conflict
        var second = await client.PostAsJsonAsync($"/api/v1/sourcing/strategies/{strategy.Id}/agree", new AgreeSourcingStrategyRequest());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AgreeStrategy_NonExistentStrategy_Returns404()
    {
        var client = factory.CreateClient();
        var nonExistentId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/api/v1/sourcing/strategies/{nonExistentId}/agree", new AgreeSourcingStrategyRequest());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SpendAnalysis_ReturnsHistoricalAndBenchmarkMetrics()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/sourcing/spend-analysis?category=IT%20and%20Telecommunication");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SpendAnalysisResponse>();
        Assert.NotNull(body);
        Assert.True(body.HistoricalSpend > 0);
        Assert.True(body.BenchmarkPrice > 0);
        Assert.False(body.TailSpendFlag);
    }
}

