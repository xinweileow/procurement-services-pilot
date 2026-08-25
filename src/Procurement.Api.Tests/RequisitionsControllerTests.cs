using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class RequisitionsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Requisition> SeedAwaitingFinalisationAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var req = new Requisition
        {
            Reference = $"REQ-{Random.Shared.Next(1000, 9999)}",
            RequestId = Guid.NewGuid(),
            Title = "PFaaS COR Capex commitment",
            Status = RequisitionStatus.AwaitingFinalisation,
            EstCost = 8000m,
        };
        db.Requisitions.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    [Fact]
    public async Task Finalise_ReturnsConflict_WhenWrongStatus()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var req = new Requisition
        {
            Reference = "REQ-DRAFT-1",
            RequestId = Guid.NewGuid(),
            Title = "Draft item",
            Status = RequisitionStatus.Draft,
            EstCost = 1000m,
        };
        db.Requisitions.Add(req);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/v1/requisitions/{req.Id}/finalise", new FinaliseRequisitionRequest { FinalSpent = 1000m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Finalise_ReturnsOk_WhenAwaitingFinalisation()
    {
        var req = await SeedAwaitingFinalisationAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/requisitions/{req.Id}/finalise", new FinaliseRequisitionRequest { FinalSpent = 7500m, Notes = "Within budget" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RequisitionResponse>();
        Assert.Equal("Finalised", body!.Status);
        Assert.Equal(7500m, body.FinalSpent);
        Assert.NotNull(body.FinalisedOn);
    }
}
