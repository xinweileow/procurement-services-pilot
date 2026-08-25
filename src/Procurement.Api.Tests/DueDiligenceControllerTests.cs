using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class DueDiligenceControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Request> SeedRequestAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var request = new Request
        {
            RequestId = $"PR-{Random.Shared.Next(1000, 9999)}",
            EstimatedValue = 85_000,
            Title = "Due Diligence Test Request",
            Description = "Testing applicability and readiness",
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
    public async Task GetDueDiligence_SourceableAndAddressable_Requires3PCAndESG()
    {
        var req = await SeedRequestAsync();
        var client = factory.CreateClient();

        // Update applicability to Sourceable + Addressable Spend
        await client.PatchAsJsonAsync($"/api/v1/requests/{req.Id}/due-diligence-applicability", new UpdateDueDiligenceApplicabilityRequest(
            SourcingType: "Sourceable",
            SpendType: "Addressable Spend"));

        var response = await client.GetAsync($"/api/v1/requests/{req.Id}/due-diligence");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DueDiligenceResponse>();

        Assert.NotNull(body);
        Assert.True(body.ThreePCRequired);
        Assert.True(body.EsgRequired);
        Assert.True(body.Ready);
    }

    [Fact]
    public async Task GetDueDiligence_NonSourceableAndAddressable_Requires3PCButNotESG()
    {
        var req = await SeedRequestAsync();
        var client = factory.CreateClient();

        // Update applicability to Non-Sourceable + Addressable Spend
        await client.PatchAsJsonAsync($"/api/v1/requests/{req.Id}/due-diligence-applicability", new UpdateDueDiligenceApplicabilityRequest(
            SourcingType: "Non-Sourceable",
            SpendType: "Addressable Spend"));

        var response = await client.GetAsync($"/api/v1/requests/{req.Id}/due-diligence");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DueDiligenceResponse>();

        Assert.NotNull(body);
        Assert.True(body.ThreePCRequired);
        Assert.False(body.EsgRequired);
    }

    [Fact]
    public async Task GetDueDiligence_NonAddressable_Neither3PCNorESGRequired()
    {
        var req = await SeedRequestAsync();
        var client = factory.CreateClient();

        // Update applicability to Non-Addressable Spend
        await client.PatchAsJsonAsync($"/api/v1/requests/{req.Id}/due-diligence-applicability", new UpdateDueDiligenceApplicabilityRequest(
            SourcingType: "Non-Sourceable",
            SpendType: "Non-Addressable Spend"));

        var response = await client.GetAsync($"/api/v1/requests/{req.Id}/due-diligence");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DueDiligenceResponse>();

        Assert.NotNull(body);
        Assert.False(body.ThreePCRequired);
        Assert.False(body.EsgRequired);
    }

    [Fact]
    public async Task GetDueDiligence_MaterialSupplierWithMissingDoc_ReturnsReadyFalse()
    {
        var req = await SeedRequestAsync();
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var record = new DueDiligenceRecord
            {
                RequestId = req.Id,
                SupplierId = Guid.NewGuid(),
                MaterialityStatus = "Material",
                TprmStatus = "Pending",
                MaterialSupplierDocsAttached = false, // missing required doc
            };
            db.DueDiligenceRecords.Add(record);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/v1/requests/{req.Id}/due-diligence");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DueDiligenceResponse>();

        Assert.NotNull(body);
        Assert.Equal("Material", body.MaterialityStatus);
        Assert.False(body.Ready);
    }

    [Fact]
    public async Task CreateSupplierException_MissingFields_Returns422()
    {
        var req = await SeedRequestAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/requests/{req.Id}/supplier-exception", new CreateSupplierExceptionRequest(
            Reason: "",
            ProcurementHeadApprovalAttachmentId: null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateSupplierException_ValidInput_Returns200WithPendingApproval()
    {
        var req = await SeedRequestAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/requests/{req.Id}/supplier-exception", new CreateSupplierExceptionRequest(
            Reason: "Urgent emergency procurement with sole proprietary provider",
            ProcurementHeadApprovalAttachmentId: Guid.NewGuid()));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SupplierExceptionResponse>();
        Assert.NotNull(body);
        Assert.Equal("pending_approval", body.Status);
    }
}
