using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Hooks.StopContainers;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.Hooks;

[RequirePermission("Hooks", "StopContainers")]
public class StopContainersEndpoint : Endpoint<StopContainersViaHookRequest, StopContainersViaHookResponse>
{
    private readonly IMediator _mediator;

    public StopContainersEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/hooks/stop-containers");
        PreProcessor<RbacPreProcessor<StopContainersViaHookRequest>>();
    }

    public override async Task HandleAsync(StopContainersViaHookRequest req, CancellationToken ct)
    {
        // Resolve EnvironmentId: from request body, or fallback to API Key's env_id claim
        var environmentId = req.EnvironmentId;

        if (string.IsNullOrEmpty(environmentId))
        {
            environmentId = HttpContext.User.FindFirst(RbacClaimTypes.EnvironmentId)?.Value;
        }

        if (string.IsNullOrEmpty(environmentId))
        {
            ThrowError("EnvironmentId is required. Provide it in the request body or use an environment-scoped API key.");
        }

        var response = await _mediator.Send(
            new StopContainersViaHookCommand(req.ProductId, req.StackDefinitionName, environmentId!, req.EnvironmentName), ct);

        if (!response.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        Response = response;
    }
}
