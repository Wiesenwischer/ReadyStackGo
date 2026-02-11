using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Hooks.RedeployStack;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.Hooks;

[RequirePermission("Hooks", "Redeploy")]
public class RedeployEndpoint : Endpoint<RedeployStackRequest, RedeployStackResponse>
{
    private readonly IMediator _mediator;

    public RedeployEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/hooks/redeploy");
        PreProcessor<RbacPreProcessor<RedeployStackRequest>>();
    }

    public override async Task HandleAsync(RedeployStackRequest req, CancellationToken ct)
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
            new RedeployStackCommand(req.StackName, environmentId!), ct);

        if (!response.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        Response = response;
    }
}
