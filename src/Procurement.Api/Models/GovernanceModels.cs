namespace Procurement.Api.Models;

public enum ApprovalGate
{
    Department,
    EtcGppc,
    GppcExco,
    Exco,
    Board,
    Cto,
    Cfo,
    Smc,
}

public enum ApprovalTaskStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>docs/kb/technical_kb.md Module M3, Entity: GovernanceDeclaration.</summary>
public sealed class GovernanceDeclaration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public bool Outsourcing { get; set; }
    public bool SingleSource { get; set; }
    public bool Emergency { get; set; }
    public string? SingleSourceCategory { get; set; }
    public string? SingleSourceReason { get; set; }
    public string? EmergencyReason { get; set; }
    public string? DisruptionImpact { get; set; }
    public string? AntiSplitJustification { get; set; }
}

/// <summary>docs/kb/technical_kb.md Module M3, Entity: RoleAssignment.</summary>
public sealed class RoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string? TechnicalContact { get; set; }
    public string ProcurementLead { get; set; } = string.Empty;
    public string TechnicalEvaluator { get; set; } = string.Empty;
    public string CommercialEvaluator { get; set; } = string.Empty;
    public string Approver { get; set; } = string.Empty;
}

/// <summary>docs/kb/technical_kb.md Module M3, Entity: ApprovalTask.</summary>
public sealed class ApprovalTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public ApprovalGate Gate { get; set; }
    public string ApproverId { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = "v1.0";
    public ApprovalTaskStatus Status { get; set; } = ApprovalTaskStatus.Pending;
    public string? DecisionComments { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
