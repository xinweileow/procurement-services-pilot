using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class EpvVouchersControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<(PurchaseOrder Po, GoodsReceipt Gr, Invoice Invoice)> SeedChainAsync(decimal poAmount, decimal invoiceAmount)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();

        var pr = new PurchaseRequisition { RequestId = Guid.NewGuid(), Status = "approved" };
        db.PurchaseRequisitions.Add(pr);

        var po = new PurchaseOrder
        {
            PurchaseRequisitionId = pr.Id,
            SupplierId = Guid.NewGuid(),
            PoNumber = $"PO-{Guid.NewGuid().ToString()[..8]}",
            Amount = poAmount,
        };
        db.PurchaseOrders.Add(po);

        var gr = new GoodsReceipt
        {
            PurchaseOrderId = po.Id,
            Grn = $"GRN-{Guid.NewGuid().ToString()[..8]}",
            QuantityReceived = 1,
            AcceptanceDate = DateTime.UtcNow.Date,
            AcceptedBy = "warehouse-01",
        };
        db.GoodsReceipts.Add(gr);

        var invoice = new Invoice
        {
            SupplierId = po.SupplierId,
            InvoiceNumber = $"INV-{Guid.NewGuid().ToString()[..8]}",
            InvoiceDate = DateTime.UtcNow.Date,
            Amount = invoiceAmount,
            Tax = 0m,
            PoNumber = po.PoNumber,
            Grn = gr.Grn,
            Category = "Exempt",
            InvoiceType = "exempt",
        };
        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();
        return (po, gr, invoice);
    }

    [Fact]
    public async Task Match_OutsideTolerance_Returns200WithMismatchDifferences()
    {
        var (po, gr, invoice) = await SeedChainAsync(poAmount: 1000m, invoiceAmount: 900m);
        var client = factory.CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/v1/epv-vouchers", new CreateEpvVoucherRequest(
            PoNumber: po.PoNumber, Grn: gr.Grn, InvoiceId: invoice.Id));
        var voucher = await createResp.Content.ReadFromJsonAsync<EpvVoucherResponse>();

        var matchResp = await client.PostAsync($"/api/v1/epv-vouchers/{voucher!.Id}/match", null);

        Assert.Equal(HttpStatusCode.OK, matchResp.StatusCode);
        var body = await matchResp.Content.ReadFromJsonAsync<EpvMatchResponse>();
        Assert.NotNull(body);
        Assert.Equal("mismatch", body!.MatchStatus);
        Assert.NotNull(body.Differences);
        Assert.Equal(-100m, body.Differences!.AmountDifference);
    }

    [Fact]
    public async Task ResolveException_InvalidResolutionValue_Returns422()
    {
        var (po, gr, invoice) = await SeedChainAsync(poAmount: 1000m, invoiceAmount: 500m);
        var client = factory.CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/v1/epv-vouchers", new CreateEpvVoucherRequest(
            PoNumber: po.PoNumber, Grn: gr.Grn, InvoiceId: invoice.Id));
        var voucher = await createResp.Content.ReadFromJsonAsync<EpvVoucherResponse>();
        await client.PostAsync($"/api/v1/epv-vouchers/{voucher!.Id}/match", null);

        Guid exceptionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            exceptionId = db.InvoiceExceptions.Single(e => e.EpvVoucherId == voucher.Id).Id;
        }

        var resolveResp = await client.PatchAsJsonAsync(
            $"/api/v1/epv-vouchers/{voucher.Id}/exceptions/{exceptionId}", new ResolveEpvExceptionRequest(
                Owner: "ap-team", Resolution: "not_a_real_resolution"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resolveResp.StatusCode);
    }

    [Fact]
    public async Task Approve_WhenNotMatched_Returns409()
    {
        var (po, gr, invoice) = await SeedChainAsync(poAmount: 1000m, invoiceAmount: 1000m);
        var client = factory.CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/v1/epv-vouchers", new CreateEpvVoucherRequest(
            PoNumber: po.PoNumber, Grn: gr.Grn, InvoiceId: invoice.Id));
        var voucher = await createResp.Content.ReadFromJsonAsync<EpvVoucherResponse>();

        var approveResp = await client.PostAsync($"/api/v1/epv-vouchers/{voucher!.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, approveResp.StatusCode);
    }

    [Fact]
    public async Task Approve_AfterMatched_Returns200WithReadyForPayment()
    {
        var (po, gr, invoice) = await SeedChainAsync(poAmount: 1000m, invoiceAmount: 1000m);
        var client = factory.CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/v1/epv-vouchers", new CreateEpvVoucherRequest(
            PoNumber: po.PoNumber, Grn: gr.Grn, InvoiceId: invoice.Id));
        var voucher = await createResp.Content.ReadFromJsonAsync<EpvVoucherResponse>();
        var matchResp = await client.PostAsync($"/api/v1/epv-vouchers/{voucher!.Id}/match", null);
        matchResp.EnsureSuccessStatusCode();

        var approveResp = await client.PostAsync($"/api/v1/epv-vouchers/{voucher.Id}/approve", null);
        approveResp.EnsureSuccessStatusCode();
        var body = await approveResp.Content.ReadFromJsonAsync<EpvVoucherApproveResponse>();

        Assert.NotNull(body);
        Assert.Equal("ready_for_payment", body!.Status);
    }
}
