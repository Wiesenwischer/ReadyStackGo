using FastEndpoints;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Application.Containers.DTOs;

namespace ReadyStackGo.API.Endpoints.Containers;

public class ListContainersRequest
{
    /// <summary>
    /// The environment ID to list containers from.
    /// </summary>
    [QueryParam]
    public string Environment { get; set; } = null!;
}

public class ListContainersEndpoint : Endpoint<ListContainersRequest, IEnumerable<ContainerDto>>
{
    public IDockerService DockerService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/containers");
        Roles("admin", "operator");
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(ListContainersRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        try
        {
            var containers = await DockerService.ListContainersAsync(req.Environment, ct);
            Response = containers;
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
