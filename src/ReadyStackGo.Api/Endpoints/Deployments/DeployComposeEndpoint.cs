using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.DeployCompose;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// Deploys a compose stack. Requires Deployments.Create permission.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Create")]
public class DeployComposeEndpoint : Endpoint<DeployComposeRequest, DeployComposeResponse>
{
    private readonly IMediator _mediator;

    public DeployComposeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/deployments");
        PreProcessor<RbacPreProcessor<DeployComposeRequest>>();
    }

    public override async Task HandleAsync(DeployComposeRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        // Set EnvironmentId for RBAC scope check
        req.EnvironmentId = environmentId;

        var response = await _mediator.Send(
            new DeployComposeCommand(environmentId, req.StackName, req.YamlContent, req.Variables, req.StackVersion, req.SessionId), ct);

        if (!response.Success && response.Message?.Contains("not found") == true)
        {
            ThrowError(response.Message);
        }

        Response = response;
    }
}
