using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.StopProductContainers;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// API request for stopping containers of a product deployment.
/// </summary>
public class StopProductContainersApiRequest
{
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    [BindFrom("productDeploymentId")]
    public string ProductDeploymentId { get; set; } = string.Empty;

    public List<string>? StackNames { get; set; }
}

/// <summary>
/// Stops containers of a product deployment. Requires Deployments.Execute permission.
/// POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/stop-containers
/// </summary>
[RequirePermission("Deployments", "Execute")]
public class StopProductContainersEndpoint : Endpoint<StopProductContainersApiRequest, StopProductContainersResponse>
{
    private readonly IMediator _mediator;

    public StopProductContainersEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/product-deployments/{productDeploymentId}/stop-containers");
        PreProcessor<RbacPreProcessor<StopProductContainersApiRequest>>();
    }

    public override async Task HandleAsync(StopProductContainersApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var productDeploymentId = Route<string>("productDeploymentId")!;

        var command = new StopProductContainersCommand(
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

            ThrowError(response.Message ?? "Stop containers failed", StatusCodes.Status400BadRequest);
        }

        Response = response;
    }
}
