using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.SetDefaultEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// POST /api/environments/{id}/default - Set an environment as default.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Environments", "Update")]
public class SetDefaultEnvironmentEndpoint : Endpoint<EmptyRequest, SetDefaultEnvironmentResponse>
{
    private readonly IMediator _mediator;

    public SetDefaultEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{id}/default");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("id")!;
        var response = await _mediator.Send(new SetDefaultEnvironmentCommand(environmentId), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to set default environment");
        }

        Response = response;
    }
}
