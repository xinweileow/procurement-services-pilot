using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Procurement.Api.Data;

namespace Procurement.Api.Tests;

/// <summary>
/// Gives each test class its own InMemory database (Program.cs registers a single fixed
/// "procurement" instance, which would otherwise leak state between test classes sharing one
/// WebApplicationFactory instance via IClassFixture).
/// </summary>
public sealed class ProcurementApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"procurement-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ProcurementDbContext>>();
            services.AddDbContext<ProcurementDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
