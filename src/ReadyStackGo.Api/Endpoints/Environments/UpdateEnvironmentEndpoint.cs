using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.UpdateEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

public class UpdateEnvironmentEndpoint : Endpoint<UpdateEnvironmentRequest, UpdateEnvironmentResponse>
{
    private readonly IMediator _mediator;

    public UpdateEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/environments/{id}");
    }

    public override async Task HandleAsync(UpdateEnvironmentRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("id")!;
        var response = await _mediator.Send(
            new UpdateEnvironmentCommand(environmentId, req.Name, req.SocketPath), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to update environment");
        }

        Response = response;
    }
}
