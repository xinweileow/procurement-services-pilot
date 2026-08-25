using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class SuppliersControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Supplier> SeedSupplierAsync(string name = "Acme Corp Sdn Bhd", string reg = "REG-12345", string tax = "TAX-99999", string? account = "11223344")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var supplier = new Supplier
        {
            LegalEntityName = name,
            RegistrationNumber = reg,
            TaxId = tax,
            Address = "456 Industrial Zone, Petaling Jaya",
            AccountNumber = account,
            BankVerified = true,
            BankStatus = "verified",
            ActiveFlag = true,
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    [Fact]
    public async Task CreateSupplier_ValidInput_Returns200WithPendingStatus()
    {
        var client = factory.CreateClient();
        var request = new CreateSupplierRequest(
            LegalEntityName: "New Supplier Global",
            RegistrationNumber: $"REG-{Random.Shared.Next(10000, 99999)}",
            TaxId: $"TAX-{Random.Shared.Next(10000, 99999)}",
            Address: "123 Jalan Ampang, Kuala Lumpur");

        var response = await client.PostAsJsonAsync("/api/v1/suppliers", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.NotNull(body);
        Assert.Equal("pending", body.Status);
        Assert.True(body.ActiveFlag);
    }

    [Fact]
    public async Task CreateSupplier_MissingFields_Returns422()
    {
        var client = factory.CreateClient();
        var request = new CreateSupplierRequest(
            LegalEntityName: "",
            RegistrationNumber: "",
            TaxId: "",
            Address: "");

        var response = await client.PostAsJsonAsync("/api/v1/suppliers", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateCheck_MatchingSupplier_Returns200WithDuplicateFoundTrueAndCandidates()
    {
        var supplier = await SeedSupplierAsync(name: "Unique Solutions Sdn Bhd", reg: "REG-DUP-001");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/suppliers/duplicate-check", new DuplicateCheckRequest(
            LegalEntityName: "Unique Solutions Sdn Bhd",
            RegistrationNumber: "REG-DUP-001"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DuplicateCheckResponse>();
        Assert.NotNull(body);
        Assert.True(body.DuplicateFound);
        Assert.NotEmpty(body.Candidates);
        Assert.Contains(body.Candidates, c => c.Id == supplier.Id);
    }

    [Fact]
    public async Task DuplicateCheck_NoMatch_Returns200WithDuplicateFoundFalse()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/suppliers/duplicate-check", new DuplicateCheckRequest(
            LegalEntityName: "Completely Non-Existent Vendor Name XYZ 999"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DuplicateCheckResponse>();
        Assert.NotNull(body);
        Assert.False(body.DuplicateFound);
        Assert.Empty(body.Candidates);
    }

    [Fact]
    public async Task UpdateBankDetails_ReturnsPendingVerificationStatus()
    {
        var supplier = await SeedSupplierAsync(name: "Bank Update Vendor");
        var client = factory.CreateClient();

        var request = new UpdateBankDetailsRequest(
            BeneficiaryName: "Bank Update Vendor Sdn Bhd",
            BankName: "Maybank Berhad",
            AccountNumber: "514012345678");

        var response = await client.PatchAsJsonAsync($"/api/v1/suppliers/{supplier.Id}/bank-details", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BankDetailsResponse>();
        Assert.NotNull(body);
        Assert.Equal("pending_verification", body.Status);
    }

    [Fact]
    public async Task GetStatus_ExistingSupplier_ReturnsStatusDetails()
    {
        var supplier = await SeedSupplierAsync(name: "Status Check Vendor");
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/suppliers/{supplier.Id}/status");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SupplierStatusResponse>();
        Assert.NotNull(body);
        Assert.True(body.Active);
        Assert.Equal("Valid", body.ThreePCStatus);
    }
}
