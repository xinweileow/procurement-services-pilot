using System.Net;

namespace Procurement.Api.Tests;

public sealed class HealthControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
