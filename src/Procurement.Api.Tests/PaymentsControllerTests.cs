using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class PaymentsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<PaymentResponse> SeedPaymentAsync(HttpClient client, decimal amount = 10_000m, string? bankDetails = "Acct 1234", string? preparerId = "preparer-01")
    {
        var resp = await client.PostAsJsonAsync("/api/v1/payments", new CreatePaymentRequest(
            EpvVoucherId: Guid.NewGuid(),
            Route: "PO",
            PayeeId: Guid.NewGuid(),
            BankDetails: bankDetails,
            Amount: amount,
            Currency: "MYR",
            PaymentDate: DateTime.UtcNow.Date,
            PreparerId: preparerId));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PaymentResponse>())!;
    }

    [Fact]
    public async Task Approve_ApproverHasSodConflictWithPreparer_Returns422()
    {
        var client = factory.CreateClient();
        var payment = await SeedPaymentAsync(client, preparerId: "user-01");

        var resp = await client.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new ApprovePaymentRequest(
            ApproverId: "user-01"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Approve_DifferentApprover_Succeeds()
    {
        var client = factory.CreateClient();
        var payment = await SeedPaymentAsync(client, preparerId: "user-01");

        var resp = await client.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new ApprovePaymentRequest(
            ApproverId: "user-02"));

        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ControlsCheck_MissingBankDetails_Returns409()
    {
        var client = factory.CreateClient();
        var payment = await SeedPaymentAsync(client, bankDetails: null);

        var resp = await client.PostAsync($"/api/v1/payments/{payment.Id}/controls-check", null);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task ControlsCheck_ExceedsApprovedLimit_Returns409()
    {
        var client = factory.CreateClient();
        var payment = await SeedPaymentAsync(client, amount: 10_000_000m);

        var resp = await client.PostAsync($"/api/v1/payments/{payment.Id}/controls-check", null);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Execute_NotYetApproved_Returns409()
    {
        var client = factory.CreateClient();
        var payment = await SeedPaymentAsync(client);

        var resp = await client.PostAsync($"/api/v1/payments/{payment.Id}/execute", null);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Execute_AfterApproval_Returns200Executed()
    {
        var client = factory.CreateClient();
        var payment = await SeedPaymentAsync(client, preparerId: "user-01");
        await client.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/approve", new ApprovePaymentRequest(ApproverId: "user-02"));

        var resp = await client.PostAsync($"/api/v1/payments/{payment.Id}/execute", null);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ExecutePaymentResponse>();

        Assert.NotNull(body);
        Assert.Equal("executed", body!.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.BankReference));
    }
}
