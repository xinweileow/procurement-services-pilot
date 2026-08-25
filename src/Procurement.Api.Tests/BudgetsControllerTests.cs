using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class BudgetsControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    private async Task SeedBudgetAsync(decimal allocated = 100_000, decimal actualSpend = 10_000)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        if (!db.Budgets.Any(b => b.CostCentre == "FIN-2040"))
        {
            var budget = new Budget
            {
                CostCentre = "FIN-2040",
                GlAccount = "GL-5000",
                FiscalYear = 2026,
                AllocatedAmount = allocated,
                ActualSpend = actualSpend,
                CostType = CostType.Capex,
            };
            db.Budgets.Add(budget);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Get_ReturnsUnprocessable_WhenFiscalYearInvalid()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/budgets?fiscalYear=99");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task AvailabilityCheck_ReturnsSufficientTrue_WhenWithinBalance()
    {
        await SeedBudgetAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/budgets/availability-check", new BudgetAvailabilityCheckRequest
        {
            CostCentre = "FIN-2040",
            GlAccount = "GL-5000",
            FiscalYear = 2026,
            RequestedAmount = 50_000,
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BudgetAvailabilityCheckResponse>();
        Assert.True(body!.Sufficient);
        Assert.Equal(0m, body.Shortfall);
    }

    [Fact]
    public async Task AvailabilityCheck_ReturnsSufficientFalse_WithShortfall_WhenExceeded()
    {
        await SeedBudgetAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/budgets/availability-check", new BudgetAvailabilityCheckRequest
        {
            CostCentre = "FIN-2040",
            GlAccount = "GL-5000",
            FiscalYear = 2026,
            RequestedAmount = 200_000,
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BudgetAvailabilityCheckResponse>();
        Assert.False(body!.Sufficient);
        Assert.True(body.Shortfall > 0);
    }
}
