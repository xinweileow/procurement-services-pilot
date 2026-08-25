using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Controllers;

[ApiController]
[Route("api/v1/requests/{requestId:guid}")]
public sealed class GovernanceController(ProcurementDbContext db) : ControllerBase
{
    [HttpPatch("governance-declarations")]
    public async Task<ActionResult<GovernanceDeclaration>> UpdateDeclarations(
        Guid requestId, [FromBody] UpdateGovernanceDeclarationsRequest body, CancellationToken ct)
    {
        if (body.SingleSource && string.IsNullOrWhiteSpace(body.SingleSourceReason))
        {
            throw new UnprocessableException("Single-source justification reason is mandatory when SingleSource is true.");
        }
        if (body.Emergency && (string.IsNullOrWhiteSpace(body.EmergencyReason) || string.IsNullOrWhiteSpace(body.DisruptionImpact)))
        {
            throw new UnprocessableException("Emergency reason and disruption impact are mandatory when Emergency is true.");
        }

        var decl = await db.GovernanceDeclarations.FirstOrDefaultAsync(g => g.RequestId == requestId, ct);
        if (decl is null)
        {
            decl = new GovernanceDeclaration { RequestId = requestId };
            db.GovernanceDeclarations.Add(decl);
        }

        decl.Outsourcing = body.Outsourcing;
        decl.SingleSource = body.SingleSource;
        decl.Emergency = body.Emergency;
        decl.SingleSourceCategory = body.SingleSourceCategory;
        decl.SingleSourceReason = body.SingleSourceReason;
        decl.EmergencyReason = body.EmergencyReason;
        decl.DisruptionImpact = body.DisruptionImpact;
        decl.AntiSplitJustification = body.AntiSplitJustification;

        await db.SaveChangesAsync(ct);
        return Ok(decl);
    }

    [HttpPatch("role-assignments")]
    public async Task<ActionResult<RoleAssignmentsResponse>> UpdateRoles(
        Guid requestId, [FromBody] UpdateRoleAssignmentsRequest body, CancellationToken ct)
    {
        var roles = new[] { body.ProcurementLead, body.TechnicalEvaluator, body.CommercialEvaluator, body.Approver }
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToLowerInvariant())
            .ToList();

        var hasConflict = roles.Distinct().Count() != roles.Count;
        if (hasConflict)
        {
            throw new UnprocessableException(
                "Segregation-of-duties conflict: Procurement lead, technical evaluator, commercial evaluator and approver must be distinct individuals.");
        }

        var assignment = await db.RoleAssignments.FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        if (assignment is null)
        {
            assignment = new RoleAssignment { RequestId = requestId };
            db.RoleAssignments.Add(assignment);
        }

        assignment.TechnicalContact = body.TechnicalContact;
        assignment.ProcurementLead = body.ProcurementLead;
        assignment.TechnicalEvaluator = body.TechnicalEvaluator;
        assignment.CommercialEvaluator = body.CommercialEvaluator;
        assignment.Approver = body.Approver;

        await db.SaveChangesAsync(ct);

        return Ok(new RoleAssignmentsResponse(
            assignment.Id, assignment.RequestId, assignment.TechnicalContact, assignment.ProcurementLead,
            assignment.TechnicalEvaluator, assignment.CommercialEvaluator, assignment.Approver, hasConflict));
    }
}
