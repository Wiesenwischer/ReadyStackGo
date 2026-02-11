using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Hooks.UpgradeViaHook;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.Hooks;

[RequirePermission("Hooks", "Upgrade")]
public class UpgradeEndpoint : Endpoint<UpgradeViaHookRequest, UpgradeViaHookResponse>
{
    private readonly IMediator _mediator;

    public UpgradeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/hooks/upgrade");
        PreProcessor<RbacPreProcessor<UpgradeViaHookRequest>>();
    }

    public override async Task HandleAsync(UpgradeViaHookRequest req, CancellationToken ct)
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
            new UpgradeViaHookCommand(req.StackName, req.TargetVersion, environmentId!, req.Variables), ct);

        if (!response.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        Response = response;
    }
}
