using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class SelfBilledControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Payment> SeedPaymentAsync(string? category)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var payment = new Payment
        {
            Reference = $"PV-{Guid.NewGuid().ToString()[..8]}",
            EpvVoucherId = Guid.NewGuid(),
            Route = "PO",
            PayeeId = Guid.NewGuid(),
            Amount = 5000m,
            PaymentDate = DateTime.UtcNow.Date,
            Category = category,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    [Fact]
    public async Task SelfBilledApplicability_NonLhdnCategory_ReturnsApplicableFalse()
    {
        var payment = await SeedPaymentAsync(category: "Non-LHDN");
        var client = factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/payments/{payment.Id}/self-billed-applicability");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<SelfBilledApplicabilityResponse>();

        Assert.NotNull(body);
        Assert.False(body!.Applicable);
    }

    [Fact]
    public async Task Resubmit_WhenNotRejected_Returns409()
    {
        var payment = await SeedPaymentAsync(category: null);
        var client = factory.CreateClient();

        Guid recordId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var record = new SelfBilledRecord { PaymentId = payment.Id, Status = "pending" };
            db.SelfBilledRecords.Add(record);
            await db.SaveChangesAsync();
            recordId = record.Id;
        }

        var resp = await client.PostAsJsonAsync($"/api/v1/self-billed-records/{recordId}/resubmit", new ResubmitSelfBilledRequest());

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_Returns401()
    {
        var payment = await SeedPaymentAsync(category: null);
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/myinvois/webhooks/response")
        {
            Content = JsonContent.Create(new MyInvoisWebhookRequest(
                SelfBilledRecordId: Guid.NewGuid(), UniqueId: "u1", Qr: "qr1", Status: "accepted")),
        };
        request.Headers.Add("X-MyInvois-Signature", "not-the-right-secret");

        var resp = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
