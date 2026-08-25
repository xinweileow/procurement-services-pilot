using System.Net;
using System.Net.Http.Json;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class RequestSubmissionTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private static CreateRequestRequest ValidRequest(decimal value = 50000, string country = "Malaysia") => new()
    {
        RequesterId = "user-2",
        RequesterRole = "IT Business Requestor",
        BusinessUnit = "Finance Transformation",
        CostCentre = "FIN-2040",
        Category = "IT and Telecommunication",
        EstimatedValue = value,
        Currency = "MYR",
        Title = "Renew platform licence",
        Description = "Renew the annual platform licence for the finance tooling.",
        Department = "Finance Transformation",
        Country = country,
        Entity = "Etiqa",
        DeliveryDate = new DateOnly(2026, 12, 31),
        Criticality = "Standard",
        EngagementPathway = "Sourcing with Contract",
    };

    private static readonly SubmitRequestRequest AllDeclarationsTrue = new()
    {
        NoConflict = true,
        ConnectedPartyDeclared = true,
        NoSplittingDeclaration = true,
        CompleteDeclaration = true,
    };

    [Fact]
    public async Task Submit_ReturnsUnprocessable_WhenDeclarationMissing()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/requests", ValidRequest());
        var request = await created.Content.ReadFromJsonAsync<RequestResponse>();

        var response = await client.PostAsJsonAsync($"/api/v1/requests/{request!.Id}/submit", new SubmitRequestRequest { NoConflict = true });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Submit_ReturnsConflict_WhenAlreadySubmitted()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/requests", ValidRequest());
        var request = await created.Content.ReadFromJsonAsync<RequestResponse>();
        await client.PostAsJsonAsync($"/api/v1/requests/{request!.Id}/submit", AllDeclarationsTrue);

        var response = await client.PostAsJsonAsync($"/api/v1/requests/{request.Id}/submit", AllDeclarationsTrue);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Submit_RoutesToGsp_WhenMalaysiaAtOrAboveThreshold()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/requests", ValidRequest(value: 150_000));
        var request = await created.Content.ReadFromJsonAsync<RequestResponse>();

        var response = await client.PostAsJsonAsync($"/api/v1/requests/{request!.Id}/submit", AllDeclarationsTrue);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<RequestResponse>();
        Assert.Equal("Gsp", submitted!.RoutingDestination);
    }

    [Fact]
    public async Task Submit_RoutesToEtiqaInternalProcurement_WhenBelowThreshold()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/requests", ValidRequest(value: 50_000));
        var request = await created.Content.ReadFromJsonAsync<RequestResponse>();

        var response = await client.PostAsJsonAsync($"/api/v1/requests/{request!.Id}/submit", AllDeclarationsTrue);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<RequestResponse>();
        Assert.Equal("EtiqaInternalProcurement", submitted!.RoutingDestination);
    }
}
