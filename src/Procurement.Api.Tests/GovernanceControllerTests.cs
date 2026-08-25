using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Api.Data;
using Procurement.Api.Models;
using Procurement.Api.Models.Dtos;

namespace Procurement.Api.Tests;

public sealed class GovernanceControllerTests(ProcurementApiFactory factory) : IClassFixture<ProcurementApiFactory>
{
    [Fact]
    public async Task RoleAssignments_ReturnsUnprocessable_WhenSodConflict()
    {
        var client = factory.CreateClient();
        var requestId = Guid.NewGuid();

        var response = await client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/role-assignments", new UpdateRoleAssignmentsRequest
        {
            ProcurementLead = "Nur Aisyah",
            TechnicalEvaluator = "Amir Rahman",
            CommercialEvaluator = "Amir Rahman", // conflict!
            Approver = "Farah Aziz",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RoleAssignments_ReturnsOk_WhenDistinctUsers()
    {
        var client = factory.CreateClient();
        var requestId = Guid.NewGuid();

        var response = await client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/role-assignments", new UpdateRoleAssignmentsRequest
        {
            ProcurementLead = "Nur Aisyah",
            TechnicalEvaluator = "Amir Rahman",
            CommercialEvaluator = "Daniel Lee",
            Approver = "Farah Aziz",
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RoleAssignmentsResponse>();
        Assert.False(body!.SodConflict);
    }

    [Fact]
    public async Task GovernanceDeclarations_ReturnsUnprocessable_WhenSingleSourceWithoutReason()
    {
        var client = factory.CreateClient();
        var requestId = Guid.NewGuid();

        var response = await client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/governance-declarations", new UpdateGovernanceDeclarationsRequest
        {
            SingleSource = true,
            SingleSourceReason = "", // missing!
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
