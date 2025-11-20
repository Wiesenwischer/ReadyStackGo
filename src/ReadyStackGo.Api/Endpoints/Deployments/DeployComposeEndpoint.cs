using FastEndpoints;
using ReadyStackGo.Application.Deployments;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// POST /api/deployments/{environmentId} - Deploy a Docker Compose stack
/// </summary>
public class DeployComposeEndpoint : Endpoint<DeployComposeRequest, DeployComposeResponse>
{
    public IDeploymentService DeploymentService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/deployments/{environmentId}");
    }

    public override async Task HandleAsync(DeployComposeRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId");
        var response = await DeploymentService.DeployComposeAsync(environmentId!, req);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Deployment failed");
        }

        Response = response;
    }
}
