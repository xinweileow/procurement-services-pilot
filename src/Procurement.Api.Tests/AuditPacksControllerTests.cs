using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class AuditPacksControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Request> SeedRequestAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var request = new Request
        {
            RequestId = $"PR-{Random.Shared.Next(1000, 9999)}",
            EstimatedValue = 85_000,
            Title = "Audit Pack Test Request",
            Description = "Testing audit pack assembly",
            Category = "IT and Telecommunication",
            Department = "IT",
            RequesterId = "u1",
            RequesterRole = "IT Business Requestor",
        };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        return request;
    }

    [Fact]
    public async Task Create_RequestNotFound_Returns404()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/audit-packs", new CreateAuditPackRequest(
            RequestId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Create_RetentionDateIsAtLeast15YearsFromCreation()
    {
        var request = await SeedRequestAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/audit-packs", new CreateAuditPackRequest(
            RequestId: request.Id));

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuditPackResponse>();

        Assert.NotNull(body);
        Assert.True(body!.RetentionDate >= DateTime.UtcNow.Date.AddYears(15));
    }
}
