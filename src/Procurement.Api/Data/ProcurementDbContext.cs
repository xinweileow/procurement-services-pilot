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

    // Module M2
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetCommitment> BudgetCommitments => Set<BudgetCommitment>();
    public DbSet<BudgetException> BudgetExceptions => Set<BudgetException>();
    public DbSet<Requisition> Requisitions => Set<Requisition>();

    // Module M3
    public DbSet<GovernanceDeclaration> GovernanceDeclarations => Set<GovernanceDeclaration>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<ApprovalTask> ApprovalTasks => Set<ApprovalTask>();

    // Module M4 / M5
    public DbSet<SourcingStrategy> SourcingStrategies => Set<SourcingStrategy>();
    public DbSet<ThreePointCheck> ThreePointChecks => Set<ThreePointCheck>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<TriageDecision> TriageDecisions => Set<TriageDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Request>().Property(r => r.Status).HasConversion<string>();
        modelBuilder.Entity<Request>().Property(r => r.RoutingDestination).HasConversion<string>();

        modelBuilder.Entity<Budget>().Property(b => b.CostType).HasConversion<string>();
        modelBuilder.Entity<BudgetCommitment>().Property(c => c.Status).HasConversion<string>();
        modelBuilder.Entity<BudgetException>().Property(e => e.Status).HasConversion<string>();
        modelBuilder.Entity<Requisition>().Property(r => r.Status).HasConversion<string>();

        modelBuilder.Entity<ApprovalTask>().Property(t => t.Gate).HasConversion<string>();
        modelBuilder.Entity<ApprovalTask>().Property(t => t.Status).HasConversion<string>();
    }
}
