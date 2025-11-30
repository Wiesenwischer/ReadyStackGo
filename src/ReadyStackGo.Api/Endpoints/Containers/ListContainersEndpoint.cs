using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Containers.ListContainers;

namespace ReadyStackGo.API.Endpoints.Containers;

public class ListContainersRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;
}

public class ListContainersEndpoint : Endpoint<ListContainersRequest, IEnumerable<ContainerDto>>
{
    private readonly IMediator _mediator;

    public ListContainersEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/containers");
        Roles("admin", "operator");
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(ListContainersRequest req, CancellationToken ct)
    {
        var environment = Query<string>("environment", false);
        if (string.IsNullOrWhiteSpace(environment))
        {
            ThrowError("Environment is required");
        }

        var result = await _mediator.Send(new ListContainersQuery(environment), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to list containers");
        }

        Response = result.Containers;
    }
}
