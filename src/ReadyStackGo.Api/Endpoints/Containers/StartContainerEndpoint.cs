using FastEndpoints;
using ReadyStackGo.Application.Containers;

namespace ReadyStackGo.API.Endpoints.Containers;

public class StartContainerRequest
{
    /// <summary>
    /// The environment ID where the container is running.
    /// </summary>
    [QueryParam]
    public string Environment { get; set; } = null!;
}

public class StartContainerEndpoint : Endpoint<StartContainerRequest, EmptyResponse>
{
    public IDockerService DockerService { get; set; } = null!;

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

        try
        {
            await DockerService.StartContainerAsync(req.Environment, id, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
