using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.Application.UseCases.Deployments.Precheck;

/// <summary>
/// Query to run deployment precheck for a stack.
/// </summary>
public record RunDeploymentPrecheckQuery(
    string EnvironmentId,
    string StackId,
    string StackName,
    Dictionary<string, string> Variables
) : IRequest<PrecheckResult>;

/// <summary>
/// Handler that orchestrates all precheck rules and aggregates results.
/// </summary>
public class RunDeploymentPrecheckHandler : IRequestHandler<RunDeploymentPrecheckQuery, PrecheckResult>
{
    private readonly IEnumerable<IDeploymentPrecheckRule> _rules;
    private readonly IProductSourceService _productSourceService;
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly ILogger<RunDeploymentPrecheckHandler> _logger;

    private static readonly TimeSpan PrecheckTimeout = TimeSpan.FromSeconds(30);

    public RunDeploymentPrecheckHandler(
        IEnumerable<IDeploymentPrecheckRule> rules,
        IProductSourceService productSourceService,
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository,
        IEnvironmentRepository environmentRepository,
        ILogger<RunDeploymentPrecheckHandler> logger)
    {
        _rules = rules;
        _productSourceService = productSourceService;
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
        _environmentRepository = environmentRepository;
        _logger = logger;
    }

    public async Task<PrecheckResult> Handle(RunDeploymentPrecheckQuery request, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(PrecheckTimeout);
        var ct = timeoutCts.Token;

        // 1. Validate environment exists
        var environmentId = EnvironmentId.FromGuid(Guid.Parse(request.EnvironmentId));
        var environment = _environmentRepository.Get(environmentId);
        if (environment == null)
        {
            return new PrecheckResult([new PrecheckItem(
                "Environment",
                PrecheckSeverity.Error,
                "Environment not found",
                $"Environment '{request.EnvironmentId}' does not exist")]);
        }

        // 2. Load stack definition
        var stackDefinition = await _productSourceService.GetStackAsync(request.StackId, ct);
        if (stackDefinition == null)
        {
            return new PrecheckResult([new PrecheckItem(
                "StackDefinition",
                PrecheckSeverity.Error,
                "Stack not found",
                $"Stack '{request.StackId}' not found in catalog")]);
        }

        // 3. Gather infrastructure context
        IReadOnlyList<ContainerDto> containers;
        IReadOnlyList<DockerVolumeRaw> volumes;
        try
        {
            var containersTask = _dockerService.ListContainersAsync(request.EnvironmentId, ct);
            var volumesTask = _dockerService.ListVolumesRawAsync(request.EnvironmentId, ct);
            await Task.WhenAll(containersTask, volumesTask);

            containers = containersTask.Result.ToList();
            volumes = volumesTask.Result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather Docker context for precheck");
            return new PrecheckResult([new PrecheckItem(
                "DockerConnection",
                PrecheckSeverity.Error,
                "Cannot reach Docker host",
                $"Failed to connect to Docker: {ex.Message}")]);
        }

        // 4. Check for existing deployment
        var existingDeployment = _deploymentRepository.GetByStackName(environmentId, request.StackName);

        // 5. Build context
        var context = new PrecheckContext
        {
            EnvironmentId = request.EnvironmentId,
            StackName = request.StackName,
            StackDefinition = stackDefinition,
            Variables = request.Variables,
            RunningContainers = containers,
            ExistingVolumes = volumes,
            ExistingDeployment = existingDeployment
        };

        // 6. Execute all rules in parallel
        var allChecks = new List<PrecheckItem>();
        var ruleTasks = _rules.Select(async rule =>
        {
            try
            {
                return await rule.ExecuteAsync(context, ct);
            }
            catch (OperationCanceledException)
            {
                return new List<PrecheckItem>
                {
                    new(rule.GetType().Name.Replace("Rule", ""),
                        PrecheckSeverity.Warning,
                        "Check timed out",
                        "Precheck rule did not complete within the timeout period")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Precheck rule {Rule} failed", rule.GetType().Name);
                return new List<PrecheckItem>
                {
                    new(rule.GetType().Name.Replace("Rule", ""),
                        PrecheckSeverity.Warning,
                        "Check failed",
                        $"Rule execution error: {ex.Message}")
                };
            }
        });

        var results = await Task.WhenAll(ruleTasks);
        foreach (var ruleItems in results)
        {
            allChecks.AddRange(ruleItems);
        }

        _logger.LogInformation(
            "Deployment precheck completed for stack '{StackName}': {ErrorCount} error(s), {WarningCount} warning(s)",
            request.StackName,
            allChecks.Count(c => c.Severity == PrecheckSeverity.Error),
            allChecks.Count(c => c.Severity == PrecheckSeverity.Warning));

        return new PrecheckResult(allChecks);
    }
}
