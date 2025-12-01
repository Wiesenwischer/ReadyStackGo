using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.SetDefaultEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

public class SetDefaultEnvironmentEndpoint : EndpointWithoutRequest<SetDefaultEnvironmentResponse>
{
    private readonly IMediator _mediator;

    public SetDefaultEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{id}/default");
    }

    public override async Task HandleAsync(CancellationToken ct)
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
