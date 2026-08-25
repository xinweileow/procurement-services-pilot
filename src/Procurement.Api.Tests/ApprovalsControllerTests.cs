using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class ApprovalsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Request> SeedRequestWithValueAsync(decimal value)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var request = new Request
        {
            RequestId = $"PR-{Random.Shared.Next(1000, 9999)}",
            EstimatedValue = value,
            Title = "Test request",
            Description = "Desc",
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
    public async Task Route_ReturnsBoardGate_WhenValueAbove5Million()
    {
        var req = await SeedRequestWithValueAsync(6_000_000);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/approvals/route", new RouteApprovalRequest { RequestId = req.Id });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteApprovalResponse>();
        Assert.Contains(body!.Gates, g => g.Gate == "Board");
    }

    [Fact]
    public async Task Decision_ReturnsConflict_WhenAlreadyDecided()
    {
        var req = await SeedRequestWithValueAsync(100_000);
        var client = factory.CreateClient();
        var routed = await client.PostAsJsonAsync("/api/v1/approvals/route", new RouteApprovalRequest { RequestId = req.Id });
        routed.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var task = db.ApprovalTasks.First(t => t.RequestId == req.Id);

        var first = await client.PostAsJsonAsync($"/api/v1/approvals/{task.Id}/decision", new ApprovalDecisionRequest { Decision = "approve" });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync($"/api/v1/approvals/{task.Id}/decision", new ApprovalDecisionRequest { Decision = "approve" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
