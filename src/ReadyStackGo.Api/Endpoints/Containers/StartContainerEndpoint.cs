using FastEndpoints;
using ReadyStackGo.Application.Containers;

namespace ReadyStackGo.API.Endpoints.Containers;

public class StartContainerEndpoint : EndpointWithoutRequest
{
    public IDockerService DockerService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/containers/{id}/start");
        AllowAnonymous(); // v0.1 has no authentication
        Options(x => x.WithTags("Containers"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("id")!;
        await DockerService.StartContainerAsync(id, ct);
    }
}
