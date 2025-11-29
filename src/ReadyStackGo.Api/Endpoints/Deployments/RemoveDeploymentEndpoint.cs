using FastEndpoints;
using ReadyStackGo.Application.Deployments;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// DELETE /api/deployments/{environmentId}/{stackName} - Remove a deployment
/// </summary>
public class RemoveDeploymentEndpoint : EndpointWithoutRequest<DeployComposeResponse>
{
    public IDeploymentService DeploymentService { get; set; } = null!;

    public override void Configure()
    {
        Delete("/api/deployments/{environmentId}/{stackName}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId");
        var stackName = Route<string>("stackName");
        var response = await DeploymentService.RemoveDeploymentAsync(environmentId!, stackName!);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to remove deployment");
        }

        Response = response;
    }
}
