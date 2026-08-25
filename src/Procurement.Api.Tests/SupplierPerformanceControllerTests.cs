using System.Net;
using System.Net.Http.Json;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class SupplierPerformanceControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    [Fact]
    public async Task CreatePerformanceRecord_SupplierNotFound_Returns404()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/suppliers/{Guid.NewGuid()}/performance-records", new CreatePerformanceRecordRequest(
            PoNumber: "PO-1", IncidentDescription: "Late delivery"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Requalify_SupplierNotFound_Returns404()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/suppliers/{Guid.NewGuid()}/requalify", new RequalifySupplierRequest(
            Trigger: "renewal"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
