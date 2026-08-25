using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class PurchaseRequisitionsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Contract> SeedContractAsync(string status = "active")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var rfx = new RfxEvent { SourcingStrategyId = Guid.NewGuid(), Status = "opened" };
        db.RfxEvents.Add(rfx);
        var award = new Award { RfxEventId = rfx.Id, RecommendedSupplierId = Guid.NewGuid(), Status = "approved" };
        db.Awards.Add(award);
        var contract = new Contract { AwardId = award.Id, SupplierId = award.RecommendedSupplierId, Status = status, VersionNumber = 1 };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return contract;
    }

    [Fact]
    public async Task Create_EmptyItems_Returns422()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/purchase-requisitions", new CreatePurchaseRequisitionRequest(
            RequestId: Guid.NewGuid(), Items: []));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Approve_ContractExpired_Returns409()
    {
        var contract = await SeedContractAsync(status: "expired");
        var client = factory.CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/v1/purchase-requisitions", new CreatePurchaseRequisitionRequest(
            RequestId: Guid.NewGuid(),
            Items: [new PurchaseRequisitionItemRequest("Laptop", 2, 5000m)],
            ContractId: contract.Id));
        var pr = await createResp.Content.ReadFromJsonAsync<PurchaseRequisitionResponse>();

        var approveResp = await client.PostAsync($"/api/v1/purchase-requisitions/{pr!.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, approveResp.StatusCode);
    }

    [Fact]
    public async Task Approve_ItemPriceDiffersFromPricebook_Returns409()
    {
        var contract = await SeedContractAsync(status: "active");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.PricebookLines.Add(new PricebookLine
            {
                ContractId = contract.Id,
                Sku = "SKU-100",
                UnitPrice = 5000m,
                Uom = "EA",
                ValidFrom = DateTime.UtcNow.Date.AddDays(-30),
                ValidTo = DateTime.UtcNow.Date.AddYears(1),
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var createResp = await client.PostAsJsonAsync("/api/v1/purchase-requisitions", new CreatePurchaseRequisitionRequest(
            RequestId: Guid.NewGuid(),
            Items: [new PurchaseRequisitionItemRequest("Laptop", 1, 4500m, Sku: "SKU-100")],
            ContractId: contract.Id));
        var pr = await createResp.Content.ReadFromJsonAsync<PurchaseRequisitionResponse>();

        var approveResp = await client.PostAsync($"/api/v1/purchase-requisitions/{pr!.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, approveResp.StatusCode);
    }

    [Fact]
    public async Task Approve_Success_AssignsPoNumberAndSetsApproved()
    {
        var client = factory.CreateClient();
        var createResp = await client.PostAsJsonAsync("/api/v1/purchase-requisitions", new CreatePurchaseRequisitionRequest(
            RequestId: Guid.NewGuid(),
            Items: [new PurchaseRequisitionItemRequest("Chairs", 10, 100m)]));
        var pr = await createResp.Content.ReadFromJsonAsync<PurchaseRequisitionResponse>();

        var approveResp = await client.PostAsync($"/api/v1/purchase-requisitions/{pr!.Id}/approve", null);
        approveResp.EnsureSuccessStatusCode();
        var body = await approveResp.Content.ReadFromJsonAsync<ApprovePurchaseRequisitionResponse>();

        Assert.NotNull(body);
        Assert.Equal("approved", body!.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.PoNumber));
    }

    [Fact]
    public async Task RecordGoodsReceipt_PoNotFound_Returns404()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/v1/purchase-orders/{Guid.NewGuid()}/goods-receipts", new RecordGoodsReceiptRequest(
            QuantityReceived: 5, AcceptanceDate: DateTime.UtcNow.Date, AcceptedBy: "warehouse-01"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task CaptureInvoice_MissingMyInvoisReferenceWhenRequired_Returns422()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/invoices", new CreateInvoiceRequest(
            SupplierId: Guid.NewGuid(), InvoiceNumber: "INV-001", InvoiceDate: DateTime.UtcNow.Date,
            Amount: 1000m, Tax: 60m, PoNumber: "PO-1", Grn: "GRN-1", Category: "General Spend"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task CaptureInvoice_Duplicate_Returns409()
    {
        var client = factory.CreateClient();
        var supplierId = Guid.NewGuid();
        var invoiceDate = DateTime.UtcNow.Date;

        var first = new CreateInvoiceRequest(
            SupplierId: supplierId, InvoiceNumber: "INV-DUP", InvoiceDate: invoiceDate,
            Amount: 2000m, Tax: 120m, PoNumber: "PO-2", Grn: "GRN-2",
            Category: "Exempt");

        var firstResp = await client.PostAsJsonAsync("/api/v1/invoices", first);
        firstResp.EnsureSuccessStatusCode();

        var secondResp = await client.PostAsJsonAsync("/api/v1/invoices", first);

        Assert.Equal(HttpStatusCode.Conflict, secondResp.StatusCode);
    }
}
