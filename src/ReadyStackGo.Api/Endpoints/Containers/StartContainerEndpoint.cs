using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Containers.StartContainer;

namespace ReadyStackGo.API.Endpoints.Containers;

public class StartContainerRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;
}

public class StartContainerEndpoint : Endpoint<StartContainerRequest, EmptyResponse>
{
    private readonly IMediator _mediator;

    public StartContainerEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/containers/{id}/start");
        Roles("admin", "operator");
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(StartContainerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var id = Route<string>("id")!;

        var result = await _mediator.Send(new StartContainerCommand(req.Environment, id), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to start container");
        }
    }
}
