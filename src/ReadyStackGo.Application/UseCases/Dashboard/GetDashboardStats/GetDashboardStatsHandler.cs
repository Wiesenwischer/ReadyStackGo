using System.Text.Json;
using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Dashboard.GetDashboardStats;

public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, GetDashboardStatsResult>
{
    private readonly IProductSourceService _productSourceService;
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;

    public GetDashboardStatsHandler(
        IProductSourceService productSourceService,
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository)
    {
        _productSourceService = productSourceService;
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
    }

    public async Task<GetDashboardStatsResult> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EnvironmentId))
        {
            return new GetDashboardStatsResult(true, new DashboardStatsDto());
        }

        try
        {
            // Get catalog data
            var products = await _productSourceService.GetProductsAsync(cancellationToken);
            var stacks = await _productSourceService.GetStacksAsync(cancellationToken);
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);

            // Count active deployments (not removed) for this environment
            var environmentId = EnvironmentId.FromGuid(Guid.Parse(request.EnvironmentId));
            var activeDeployments = _deploymentRepository
                .GetByEnvironment(environmentId)
                .Count(d => d.Status != DeploymentStatus.Removed);

            var totalProducts = products.Count();
            var totalStacks = stacks.Count();
            var stats = new DashboardStatsDto
            {
                TotalProducts = totalProducts,
                TotalStacks = totalStacks,
                DeployedStacks = activeDeployments,
                NotDeployedStacks = Math.Max(0, totalStacks - activeDeployments),
                TotalContainers = containers.Count(),
                RunningContainers = containers.Count(c => c.State == "running"),
                StoppedContainers = containers.Count(c => c.State != "running")
            };

            return new GetDashboardStatsResult(true, stats);
        }
        catch (JsonException ex)
        {
            return new GetDashboardStatsResult(false, new DashboardStatsDto(),
                $"Configuration error: Unable to read configuration file. Details: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return new GetDashboardStatsResult(false, new DashboardStatsDto(), ex.Message);
        }
        catch (Exception ex)
        {
            return new GetDashboardStatsResult(false, new DashboardStatsDto(), $"Unexpected error: {ex.Message}");
        }
    }
}
