using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.RestartProductContainers;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// API request for restarting containers of a product deployment.
/// </summary>
public class RestartProductContainersApiRequest
{
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    [BindFrom("productDeploymentId")]
    public string ProductDeploymentId { get; set; } = string.Empty;

    public List<string>? StackNames { get; set; }
}

/// <summary>
/// Restarts containers of a product deployment (Stop + Start). Requires Deployments.Execute permission.
/// POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/restart-containers
/// </summary>
[RequirePermission("Deployments", "Execute")]
public class RestartProductContainersEndpoint : Endpoint<RestartProductContainersApiRequest, RestartProductContainersResponse>
{
    private readonly IMediator _mediator;

    public RestartProductContainersEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/product-deployments/{productDeploymentId}/restart-containers");
        PreProcessor<RbacPreProcessor<RestartProductContainersApiRequest>>();
    }

    public override async Task HandleAsync(RestartProductContainersApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var productDeploymentId = Route<string>("productDeploymentId")!;

        var command = new RestartProductContainersCommand(
            environmentId,
            productDeploymentId,
            req.StackNames);

        var response = await _mediator.Send(command, ct);

        if (!response.Success)
        {
            if (response.Message?.Contains("not found") == true)
            {
                ThrowError(response.Message, StatusCodes.Status404NotFound);
            }

            ThrowError(response.Message ?? "Restart containers failed", StatusCodes.Status400BadRequest);
        }

        Response = response;
    }
}
