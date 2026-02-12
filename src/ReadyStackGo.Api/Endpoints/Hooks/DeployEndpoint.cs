using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Hooks.DeployStack;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.Hooks;

[RequirePermission("Hooks", "Deploy")]
public class DeployEndpoint : Endpoint<DeployViaHookRequest, DeployViaHookResponse>
{
    private readonly IMediator _mediator;

    public DeployEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/hooks/deploy");
        PreProcessor<RbacPreProcessor<DeployViaHookRequest>>();
    }

    public override async Task HandleAsync(DeployViaHookRequest req, CancellationToken ct)
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
            new DeployViaHookCommand(req.StackId, req.StackName, environmentId!, req.Variables), ct);

        if (!response.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        Response = response;
    }
}
