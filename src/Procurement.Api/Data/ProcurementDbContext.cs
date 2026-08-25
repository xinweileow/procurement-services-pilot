using Microsoft.EntityFrameworkCore;
using Procurement.Api.Models;

namespace Procurement.Api.Data;

/// <summary>
/// EF Core context for the Procurement Service. Uses the InMemory provider for now
/// (no real database has been provisioned yet for this target repo) — swapping to a real
/// relational provider later is a one-line change here plus a connection string, not a
/// data-access rewrite. DbSets are added incrementally, one module at a time, by each
/// ticket that introduces a new entity.
/// </summary>
public sealed class ProcurementDbContext(DbContextOptions<ProcurementDbContext> options) : DbContext(options)
{
    // Module M1
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Request>().Property(r => r.Status).HasConversion<string>();
        modelBuilder.Entity<Request>().Property(r => r.RoutingDestination).HasConversion<string>();
    }
}
