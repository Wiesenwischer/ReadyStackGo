using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Health.GetEnvironmentHealthSummary;

/// <summary>
/// Handler for GetEnvironmentHealthSummaryQuery.
/// Returns full stack health (including services) for all deployments in the environment.
/// </summary>
public class GetEnvironmentHealthSummaryHandler
    : IRequestHandler<GetEnvironmentHealthSummaryQuery, GetEnvironmentHealthSummaryResponse>
{
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly ILogger<GetEnvironmentHealthSummaryHandler> _logger;

    public GetEnvironmentHealthSummaryHandler(
        IHealthSnapshotRepository healthSnapshotRepository,
        IEnvironmentRepository environmentRepository,
        IDeploymentRepository deploymentRepository,
        IProductDeploymentRepository productDeploymentRepository,
        ILogger<GetEnvironmentHealthSummaryHandler> logger)
    {
        _healthSnapshotRepository = healthSnapshotRepository;
        _environmentRepository = environmentRepository;
        _deploymentRepository = deploymentRepository;
        _productDeploymentRepository = productDeploymentRepository;
        _logger = logger;
    }

    public Task<GetEnvironmentHealthSummaryResponse> Handle(
        GetEnvironmentHealthSummaryQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting environment health summary for {EnvironmentId}", request.EnvironmentId);

        // Parse environment ID
        if (!Guid.TryParse(request.EnvironmentId, out var environmentGuid))
        {
            return Task.FromResult(
                GetEnvironmentHealthSummaryResponse.Failure("Invalid environment ID format"));
        }

        var environmentId = EnvironmentId.FromGuid(environmentGuid);

        // Get environment to verify it exists
        var environment = _environmentRepository.Get(environmentId);
        if (environment == null)
        {
            return Task.FromResult(
                GetEnvironmentHealthSummaryResponse.Failure($"Environment {request.EnvironmentId} not found"));
        }

        // Get latest snapshots via repository
        var snapshots = _healthSnapshotRepository.GetLatestForEnvironment(environmentId);

        // Get active deployment IDs (exclude removed deployments)
        var activeDeploymentIds = _deploymentRepository
            .GetByEnvironment(environmentId)
            .Where(d => d.Status != DeploymentStatus.Removed)
            .Select(d => d.Id)
            .ToHashSet();

        // Filter snapshots to only include active deployments
        var activeSnapshots = snapshots.Where(s => activeDeploymentIds.Contains(s.DeploymentId)).ToList();

        // Use domain value object for aggregate counts
        var summary = EnvironmentHealthSummary.FromSnapshots(environment, activeSnapshots);

        // Build product deployment lookup: DeploymentId → (ProductDeploymentId, ProductDisplayName)
        var productDeployments = _productDeploymentRepository.GetByEnvironment(environmentId);
        var deploymentToProduct = new Dictionary<string, (string ProductDeploymentId, string ProductDisplayName)>();
        foreach (var pd in productDeployments.Where(p => !p.IsTerminal))
        {
            foreach (var stack in pd.Stacks.Where(s => s.DeploymentId != null))
            {
                deploymentToProduct[stack.DeploymentId!.Value.ToString()] =
                    (pd.Id.Value.ToString(), pd.ProductDisplayName);
            }
        }

        // Map snapshots directly to StackHealthDto (full detail including services)
        var stackDtos = activeSnapshots.Select(snapshot =>
        {
            var stackDto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, environmentId);
            var deploymentIdStr = snapshot.DeploymentId.Value.ToString();
            if (deploymentToProduct.TryGetValue(deploymentIdStr, out var productInfo))
            {
                stackDto.ProductDeploymentId = productInfo.ProductDeploymentId;
                stackDto.ProductDisplayName = productInfo.ProductDisplayName;
            }
            return stackDto;
        }).ToList();

        var result = new EnvironmentHealthSummaryDto
        {
            EnvironmentId = environmentId.Value.ToString(),
            EnvironmentName = environment.Name,
            TotalStacks = summary.TotalStacks,
            HealthyCount = summary.HealthyCount,
            DegradedCount = summary.DegradedCount,
            UnhealthyCount = summary.UnhealthyCount,
            Stacks = stackDtos
        };

        return Task.FromResult(GetEnvironmentHealthSummaryResponse.Ok(result));
    }
}
