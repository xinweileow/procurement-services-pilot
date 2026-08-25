using System.Net;
using System.Net.Http.Json;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class RequestsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private static CreateRequestRequest ValidRequest() => new()
    {
        RequesterId = "user-1",
        RequesterRole = "IT Business Requestor",
        BusinessUnit = "Finance Transformation",
        CostCentre = "FIN-2040",
        Category = "IT and Telecommunication",
        EstimatedValue = 50000,
        Currency = "MYR",
        Title = "Renew platform licence",
        Description = "Renew the annual platform licence for the finance tooling.",
        Department = "Finance Transformation",
        Country = "Malaysia",
        Entity = "Etiqa",
        DeliveryDate = new DateOnly(2026, 12, 31),
        Criticality = "Standard",
        EngagementPathway = "Sourcing with Contract",
    };

    [Fact]
    public async Task Create_ReturnsUnprocessable_WhenTitleMissing()
    {
        var client = factory.CreateClient();
        var body = ValidRequest();
        body.Title = string.Empty;

        var response = await client.PostAsJsonAsync("/api/v1/requests", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsUnprocessable_WhenCategoryNotAllowedForRole()
    {
        var client = factory.CreateClient();
        var body = ValidRequest();
        body.Category = "General Spend"; // not in IT Business Requestor's allowed set

        var response = await client.PostAsJsonAsync("/api/v1/requests", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_ForUnknownId()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ThenGetById_RoundTrips()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/requests", ValidRequest());
        created.EnsureSuccessStatusCode();
        var createdBody = await created.Content.ReadFromJsonAsync<RequestResponse>();

        var response = await client.GetAsync($"/api/v1/requests/{createdBody!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<RequestResponse>();
        Assert.Equal("Draft", fetched!.Status);
    }
}
