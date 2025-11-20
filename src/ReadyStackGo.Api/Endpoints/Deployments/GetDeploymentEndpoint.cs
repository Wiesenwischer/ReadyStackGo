using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Deployments;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// GET /api/deployments/{environmentId}/{stackName} - Get deployment details
/// </summary>
public class GetDeploymentEndpoint : EndpointWithoutRequest<GetDeploymentResponse>
{
    public IDeploymentService DeploymentService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/deployments/{environmentId}/{stackName}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId");
        var stackName = Route<string>("stackName");
        var response = await DeploymentService.GetDeploymentAsync(environmentId!, stackName!);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Deployment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}
