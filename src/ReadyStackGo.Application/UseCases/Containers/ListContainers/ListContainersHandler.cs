using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.UseCases.Containers.ListContainers;

public class ListContainersHandler : IRequestHandler<ListContainersQuery, ListContainersResult>
{
    private readonly IDockerService _dockerService;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;

    public ListContainersHandler(IDockerService dockerService, IHealthSnapshotRepository healthSnapshotRepository)
    {
        _dockerService = dockerService;
        _healthSnapshotRepository = healthSnapshotRepository;
    }

    public async Task<ListContainersResult> Handle(ListContainersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);

            // Enrich containers with RSGO health monitoring data
            var enriched = EnrichWithHealthData(request.EnvironmentId, containers);

            return new ListContainersResult(true, enriched);
        }
        catch (InvalidOperationException ex)
        {
            return new ListContainersResult(false, Enumerable.Empty<ContainerDto>(), ex.Message);
        }
    }

    private IEnumerable<ContainerDto> EnrichWithHealthData(string environmentId, IEnumerable<ContainerDto> containers)
    {
        if (!Guid.TryParse(environmentId, out var envGuid))
            return containers;

        var snapshots = _healthSnapshotRepository.GetLatestForEnvironment(
            EnvironmentId.FromGuid(envGuid));

        // Build lookup: container name → RSGO health status
        var healthLookup = new Dictionary<string, HealthStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots)
        {
            foreach (var service in snapshot.Self.Services)
            {
                if (!string.IsNullOrEmpty(service.ContainerName))
                    healthLookup[service.ContainerName] = service.Status;
            }
        }

        if (healthLookup.Count == 0)
            return containers;

        return containers.Select(c =>
        {
            // Match by container name (strip leading '/' from Docker names)
            var name = c.Name.TrimStart('/');
            if (healthLookup.TryGetValue(name, out var rsgoStatus))
            {
                return c with { HealthStatus = rsgoStatus.Name.ToLowerInvariant() };
            }
            return c;
        });
    }
}
