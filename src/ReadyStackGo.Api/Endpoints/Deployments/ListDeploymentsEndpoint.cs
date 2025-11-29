using FastEndpoints;
using ReadyStackGo.Application.Deployments;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// GET /api/deployments/{environmentId} - List all deployments in an environment
/// </summary>
public class ListDeploymentsEndpoint : EndpointWithoutRequest<ListDeploymentsResponse>
{
    public IDeploymentService DeploymentService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/deployments/{environmentId}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId");
        var response = await DeploymentService.ListDeploymentsAsync(environmentId!);

        Response = response;
    }
}
