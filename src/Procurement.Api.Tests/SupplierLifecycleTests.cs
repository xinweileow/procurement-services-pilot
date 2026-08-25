using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class SupplierLifecycleTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task<Supplier> SeedSupplierAsync(string name = "Lifecycle Test Vendor")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var supplier = new Supplier
        {
            LegalEntityName = name,
            RegistrationNumber = $"REG-{Random.Shared.Next(10000, 99999)}",
            TaxId = $"TAX-{Random.Shared.Next(10000, 99999)}",
            Address = "789 Commercial Center, Cyberjaya",
            ActiveFlag = true,
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    [Fact]
    public async Task DeactivateSupplier_SetsActiveFlagToFalse()
    {
        var supplier = await SeedSupplierAsync("Vendor to Deactivate");
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/v1/suppliers/{supplier.Id}/deactivate", null);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.NotNull(body);
        Assert.False(body.ActiveFlag);

        // Verify status endpoint reflects deactivation
        var statusResponse = await client.GetAsync($"/api/v1/suppliers/{supplier.Id}/status");
        statusResponse.EnsureSuccessStatusCode();
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<SupplierStatusResponse>();
        Assert.NotNull(statusBody);
        Assert.False(statusBody.Active);
    }

    [Fact]
    public async Task RequalifySupplier_SchedulesRequalification()
    {
        var supplier = await SeedSupplierAsync("Vendor to Requalify");
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/v1/suppliers/{supplier.Id}/requalify", null);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ExpiringDocuments_ReturnsDocumentsWithinWindow()
    {
        var supplier = await SeedSupplierAsync("Vendor with Expiring Doc");
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var doc = new SupplierDocument
            {
                SupplierId = supplier.Id,
                DocumentType = "CompanyRegistration",
                FileName = "SSM_Certificate_2026.pdf",
                ExpiryDateUtc = DateTime.UtcNow.AddDays(15), // within 30-day window
            };
            db.SupplierDocuments.Add(doc);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/suppliers/expiring-documents?windowDays=30");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<List<ExpiringDocumentDto>>();
        Assert.NotNull(body);
        Assert.Contains(body, d => d.SupplierId == supplier.Id);
    }
}
