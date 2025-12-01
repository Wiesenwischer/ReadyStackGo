using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Containers.StopContainer;

namespace ReadyStackGo.API.Endpoints.Containers;

public class StopContainerRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;
}

public class StopContainerEndpoint : Endpoint<StopContainerRequest, EmptyResponse>
{
    private readonly IMediator _mediator;

    public StopContainerEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/containers/{id}/stop");
        Roles("admin", "operator");
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(StopContainerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var id = Route<string>("id")!;

        var result = await _mediator.Send(new StopContainerCommand(req.Environment, id), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to stop container");
        }
    }
}
