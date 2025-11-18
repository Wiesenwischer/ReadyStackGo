using FastEndpoints;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Application.Containers.DTOs;

namespace ReadyStackGo.API.Endpoints.Containers;

public class ListContainersEndpoint : Endpoint<EmptyRequest, IEnumerable<ContainerDto>>
{
    public IDockerService DockerService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/containers");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var containers = await DockerService.ListContainersAsync(ct);
        Response = containers;
    }
}
