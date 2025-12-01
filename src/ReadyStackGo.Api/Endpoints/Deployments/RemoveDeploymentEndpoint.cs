using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class RemoveDeploymentRequest
{
    /// <summary>
    /// Environment ID for RBAC scope check (from route).
    /// </summary>
    public string? EnvironmentId { get; set; }
}

/// <summary>
/// Removes a deployment. Requires Deployments.Delete permission.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Delete")]
public class RemoveDeploymentEndpoint : Endpoint<RemoveDeploymentRequest, DeployComposeResponse>
{
    private readonly IMediator _mediator;

    public RemoveDeploymentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/deployments/{environmentId}/{stackName}");
        PreProcessor<RbacPreProcessor<RemoveDeploymentRequest>>();
    }

    public override async Task HandleAsync(RemoveDeploymentRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var stackName = Route<string>("stackName")!;
        req.EnvironmentId = environmentId;

        var response = await _mediator.Send(new RemoveDeploymentCommand(environmentId, stackName), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to remove deployment");
        }

        Response = response;
    }
}
