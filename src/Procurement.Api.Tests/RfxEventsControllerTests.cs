using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class RfxEventsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<SourcingStrategy> SeedStrategyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var strategy = new SourcingStrategy
        {
            RequestId = Guid.NewGuid(),
            RecommendedRoute = "RFQ",
            Status = "agreed",
        };
        db.SourcingStrategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy;
    }

    private async Task<Supplier> SeedSupplierAsync(bool active = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var supplier = new Supplier
        {
            LegalEntityName = $"Supplier {Guid.NewGuid().ToString()[..6]}",
            RegistrationNumber = $"REG-{Random.Shared.Next(10000, 99999)}",
            TaxId = $"TAX-{Random.Shared.Next(10000, 99999)}",
            Address = "123 Business Way",
            ActiveFlag = active,
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    [Fact]
    public async Task CreateRfxEvent_ValidStrategy_ReturnsDraftEvent()
    {
        var strategy = await SeedStrategyAsync();
        var client = factory.CreateClient();

        var request = new CreateRfxEventRequest(
            SourcingStrategyId: strategy.Id,
            TechnicalTemplateId: Guid.NewGuid(),
            CommercialTemplateId: Guid.NewGuid(),
            TenderType: "open");

        var response = await client.PostAsJsonAsync("/api/v1/rfx-events", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RfxEventResponse>();
        Assert.NotNull(body);
        Assert.Equal("draft", body.Status);
    }

    [Fact]
    public async Task PublishRfxEvent_IncompleteTemplates_Returns422()
    {
        var strategy = await SeedStrategyAsync();
        var client = factory.CreateClient();

        // Create without technical/commercial templates
        var createReq = new CreateRfxEventRequest(SourcingStrategyId: strategy.Id);
        var createResp = await client.PostAsJsonAsync("/api/v1/rfx-events", createReq);
        createResp.EnsureSuccessStatusCode();
        var rfx = await createResp.Content.ReadFromJsonAsync<RfxEventResponse>();

        var publishResp = await client.PostAsync($"/api/v1/rfx-events/{rfx!.Id}/publish", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, publishResp.StatusCode);
    }

    [Fact]
    public async Task PublishRfxEvent_CompleteTemplates_ReturnsPublishedStatus()
    {
        var strategy = await SeedStrategyAsync();
        var client = factory.CreateClient();

        var createReq = new CreateRfxEventRequest(
            SourcingStrategyId: strategy.Id,
            TechnicalTemplateId: Guid.NewGuid(),
            CommercialTemplateId: Guid.NewGuid());
        var createResp = await client.PostAsJsonAsync("/api/v1/rfx-events", createReq);
        createResp.EnsureSuccessStatusCode();
        var rfx = await createResp.Content.ReadFromJsonAsync<RfxEventResponse>();

        var publishResp = await client.PostAsync($"/api/v1/rfx-events/{rfx!.Id}/publish", null);
        publishResp.EnsureSuccessStatusCode();
        var pubBody = await publishResp.Content.ReadFromJsonAsync<PublishRfxResponse>();
        Assert.NotNull(pubBody);
        Assert.Equal("published", pubBody.Status);
    }

    [Fact]
    public async Task InviteSuppliers_InactiveSupplier_Returns422()
    {
        var strategy = await SeedStrategyAsync();
        var inactiveSupplier = await SeedSupplierAsync(active: false);
        var client = factory.CreateClient();

        var createReq = new CreateRfxEventRequest(SourcingStrategyId: strategy.Id);
        var createResp = await client.PostAsJsonAsync("/api/v1/rfx-events", createReq);
        var rfx = await createResp.Content.ReadFromJsonAsync<RfxEventResponse>();

        var inviteResp = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx!.Id}/invitations", new InviteSuppliersRequest(
            SupplierIds: [inactiveSupplier.Id]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, inviteResp.StatusCode);
    }

    [Fact]
    public async Task SubmitProposal_PastDeadlineWithoutException_Returns409()
    {
        var strategy = await SeedStrategyAsync();
        var supplier = await SeedSupplierAsync(active: true);
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var rfx = new RfxEvent
            {
                SourcingStrategyId = strategy.Id,
                Status = "published",
                OpeningDateUtc = DateTime.UtcNow.AddDays(-14),
                ClosingDateUtc = DateTime.UtcNow.AddDays(-1), // Passed
            };
            db.RfxEvents.Add(rfx);
            await db.SaveChangesAsync();

            var response = await client.PostAsJsonAsync($"/api/v1/rfx-events/{rfx.Id}/submissions", new SubmitProposalRequest(
                SupplierId: supplier.Id,
                TechnicalProposal: "Tech spec doc",
                CommercialProposal: "Pricing schedule",
                LateExceptionGranted: false));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    [Fact]
    public async Task OpenProposals_BeforeDeadline_Returns403()
    {
        var strategy = await SeedStrategyAsync();
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var rfx = new RfxEvent
            {
                SourcingStrategyId = strategy.Id,
                Status = "published",
                OpeningDateUtc = DateTime.UtcNow.AddDays(-1),
                ClosingDateUtc = DateTime.UtcNow.AddDays(7), // Future closing date
            };
            db.RfxEvents.Add(rfx);
            await db.SaveChangesAsync();

            var response = await client.PostAsync($"/api/v1/rfx-events/{rfx.Id}/open", null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task OpenProposals_AfterDeadline_Returns200WithOpenedStatus()
    {
        var strategy = await SeedStrategyAsync();
        var supplier = await SeedSupplierAsync(active: true);
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var rfx = new RfxEvent
            {
                SourcingStrategyId = strategy.Id,
                Status = "published",
                OpeningDateUtc = DateTime.UtcNow.AddDays(-14),
                ClosingDateUtc = DateTime.UtcNow.AddDays(-1), // Passed
            };
            db.RfxEvents.Add(rfx);
            var sub = new RfxSubmission
            {
                RfxEventId = rfx.Id,
                SupplierId = supplier.Id,
                TechnicalProposal = "Tech solution",
                CommercialProposal = "Commercial bid",
                BidStatus = "submitted",
            };
            db.RfxSubmissions.Add(sub);
            await db.SaveChangesAsync();

            var response = await client.PostAsync($"/api/v1/rfx-events/{rfx.Id}/open", null);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<OpenProposalsResponse>();

            Assert.NotNull(body);
            Assert.NotEmpty(body.Submissions);
            Assert.Equal("opened", body.Submissions[0].BidStatus);
        }
    }
}
