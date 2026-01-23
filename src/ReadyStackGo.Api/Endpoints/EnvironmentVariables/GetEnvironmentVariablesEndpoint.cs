using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.EnvironmentVariables;
using ReadyStackGo.Application.UseCases.EnvironmentVariables.GetEnvironmentVariables;

namespace ReadyStackGo.API.Endpoints.EnvironmentVariables;

/// <summary>
/// GET /api/environments/{environmentId}/variables - Get environment variables.
/// </summary>
[RequirePermission("Environments", "Read")]
public class GetEnvironmentVariablesEndpoint : Endpoint<GetEnvironmentVariablesRequest, GetEnvironmentVariablesResponse>
{
    private readonly IMediator _mediator;

    public GetEnvironmentVariablesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/variables");
        PreProcessor<RbacPreProcessor<GetEnvironmentVariablesRequest>>();
    }

    public override async Task HandleAsync(GetEnvironmentVariablesRequest req, CancellationToken ct)
    {
        Response = await _mediator.Send(new GetEnvironmentVariablesQuery(req.EnvironmentId), ct);
    }
}

public record GetEnvironmentVariablesRequest(string EnvironmentId);
