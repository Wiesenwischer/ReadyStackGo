using Microsoft.Extensions.Logging;
using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Application.UseCases.Containers.ListContainers;

public class ListContainersHandler : IRequestHandler<ListContainersQuery, ListContainersResult>
{
    private readonly IDockerService _dockerService;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly ILogger<ListContainersHandler> _logger;

    public ListContainersHandler(
        IDockerService dockerService,
        IHealthSnapshotRepository healthSnapshotRepository,
        ILogger<ListContainersHandler> logger)
    {
        _dockerService = dockerService;
        _healthSnapshotRepository = healthSnapshotRepository;
        _logger = logger;
    }

    public async Task<ListContainersResult> Handle(ListContainersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);
            var containerList = containers.ToList();

            // Enrich containers with RSGO health monitoring data
            var enriched = EnrichWithHealthData(request.EnvironmentId, containerList);

            return new ListContainersResult(true, enriched);
        }
        catch (InvalidOperationException ex)
        {
            return new ListContainersResult(false, Enumerable.Empty<ContainerDto>(), ex.Message);
        }
    }

    private List<ContainerDto> EnrichWithHealthData(string environmentId, List<ContainerDto> containers)
    {
        if (!Guid.TryParse(environmentId, out var envGuid))
            return containers;

        IEnumerable<HealthSnapshot> snapshots;
        try
        {
            snapshots = _healthSnapshotRepository.GetLatestForEnvironment(
                EnvironmentId.FromGuid(envGuid));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load health snapshots for container enrichment");
            return containers;
        }

        // Build lookup: container name → RSGO health status (latest snapshot wins)
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
            var name = c.Name.TrimStart('/');
            if (healthLookup.TryGetValue(name, out var rsgoStatus))
            {
                return c with { HealthStatus = rsgoStatus.Name.ToLowerInvariant() };
            }
            return c;
        }).ToList();
    }
}
