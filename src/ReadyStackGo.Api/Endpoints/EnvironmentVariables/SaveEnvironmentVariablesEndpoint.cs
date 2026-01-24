using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.EnvironmentVariables;
using ReadyStackGo.Application.UseCases.EnvironmentVariables.SaveEnvironmentVariables;

namespace ReadyStackGo.API.Endpoints.EnvironmentVariables;

/// <summary>
/// POST /api/environments/{environmentId}/variables - Save environment variables.
/// </summary>
[RequirePermission("Environments", "Write")]
public class SaveEnvironmentVariablesEndpoint : Endpoint<SaveEnvironmentVariablesRequest, SaveEnvironmentVariablesResponse>
{
    private readonly IMediator _mediator;

    public SaveEnvironmentVariablesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/variables");
        PreProcessor<RbacPreProcessor<SaveEnvironmentVariablesRequest>>();
    }

    public override async Task HandleAsync(SaveEnvironmentVariablesRequest req, CancellationToken ct)
    {
        Response = await _mediator.Send(
            new SaveEnvironmentVariablesCommand(req.EnvironmentId, req.Variables), ct);
    }
}

public record SaveEnvironmentVariablesRequest(
    string EnvironmentId,
    Dictionary<string, string> Variables);
