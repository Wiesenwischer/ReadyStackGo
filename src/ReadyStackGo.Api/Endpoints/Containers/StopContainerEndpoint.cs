using FastEndpoints;
using ReadyStackGo.Application.Containers;

namespace ReadyStackGo.API.Endpoints.Containers;

public class StopContainerEndpoint : EndpointWithoutRequest
{
    public IDockerService DockerService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/containers/{id}/stop");
        Roles("admin", "operator");
        Options(x => x.WithTags("Containers"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("id")!;
        await DockerService.StopContainerAsync(id, ct);
    }
}
