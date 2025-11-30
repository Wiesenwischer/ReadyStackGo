using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.CreateEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

public class CreateEnvironmentEndpoint : Endpoint<CreateEnvironmentRequest, CreateEnvironmentResponse>
{
    private readonly IMediator _mediator;

    public CreateEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments");
    }

    public override async Task HandleAsync(CreateEnvironmentRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new CreateEnvironmentCommand(req.Id, req.Name, req.SocketPath), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to create environment");
        }

        Response = response;
    }
}
