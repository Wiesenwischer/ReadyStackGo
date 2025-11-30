using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.DeleteEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

public class DeleteEnvironmentEndpoint : EndpointWithoutRequest<DeleteEnvironmentResponse>
{
    private readonly IMediator _mediator;

    public DeleteEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/environments/{id}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<string>("id")!;
        var response = await _mediator.Send(new DeleteEnvironmentCommand(environmentId), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to delete environment");
        }

        Response = response;
    }
}
