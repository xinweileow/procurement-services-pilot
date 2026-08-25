namespace Procurement.Api.Models.Dtos;

public sealed class UpdateGovernanceDeclarationsRequest
{
    public bool Outsourcing { get; set; }
    public bool SingleSource { get; set; }
    public bool Emergency { get; set; }
    public string? SingleSourceCategory { get; set; }
    public string? SingleSourceReason { get; set; }
    public string? EmergencyReason { get; set; }
    public string? DisruptionImpact { get; set; }
    public string? AntiSplitJustification { get; set; }
}

public sealed class UpdateRoleAssignmentsRequest
{
    public string? TechnicalContact { get; set; }
    public string ProcurementLead { get; set; } = string.Empty;
    public string TechnicalEvaluator { get; set; } = string.Empty;
    public string CommercialEvaluator { get; set; } = string.Empty;
    public string Approver { get; set; } = string.Empty;
}

public sealed record RoleAssignmentsResponse(
    Guid Id,
    Guid RequestId,
    string? TechnicalContact,
    string ProcurementLead,
    string TechnicalEvaluator,
    string CommercialEvaluator,
    string Approver,
    bool SodConflict);

public sealed class RouteApprovalRequest
{
    public Guid RequestId { get; set; }
}

public sealed record RouteApprovalResponse(IReadOnlyList<ApprovalGateDto> Gates);

public sealed record ApprovalGateDto(string Gate, string ApproverId);

public sealed class ApprovalDecisionRequest
{
    public string Decision { get; set; } = string.Empty; // approve | reject
    public string? Comments { get; set; }
}

public sealed record ApprovalTaskResponse(
    Guid Id,
    Guid RequestId,
    string Gate,
    string ApproverId,
    string Status,
    string? DecisionComments,
    DateTime? DecidedAtUtc);
