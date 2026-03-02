using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Hooks.RestartContainers;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.Hooks;

[RequirePermission("Hooks", "RestartContainers")]
public class RestartContainersEndpoint : Endpoint<RestartContainersViaHookRequest, RestartContainersViaHookResponse>
{
    private readonly IMediator _mediator;

    public RestartContainersEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/hooks/restart-containers");
        PreProcessor<RbacPreProcessor<RestartContainersViaHookRequest>>();
    }

    public override async Task HandleAsync(RestartContainersViaHookRequest req, CancellationToken ct)
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
            new RestartContainersViaHookCommand(req.ProductId, req.StackDefinitionName, environmentId!), ct);

        if (!response.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        Response = response;
    }
}
