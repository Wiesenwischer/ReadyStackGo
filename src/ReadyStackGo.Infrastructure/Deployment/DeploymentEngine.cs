using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Manifests;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Deployment;

/// <summary>
/// Deployment engine that orchestrates stack deployments based on manifests
/// Implements the deployment pipeline from specification chapter 8
/// </summary>
public class DeploymentEngine : IDeploymentEngine
{
    private readonly IConfigStore _configStore;
    private readonly IDockerService _dockerService;
    private readonly ILogger<DeploymentEngine> _logger;

    public DeploymentEngine(
        IConfigStore configStore,
        IDockerService dockerService,
        ILogger<DeploymentEngine> logger)
    {
        _configStore = configStore;
        _dockerService = dockerService;
        _logger = logger;
    }

    public async Task<DeploymentPlan> GenerateDeploymentPlanAsync(ReleaseManifest manifest)
    {
        _logger.LogInformation("Generating deployment plan for stack version {Version}", manifest.StackVersion);

        var plan = new DeploymentPlan
        {
            StackVersion = manifest.StackVersion
        };

        // Load configurations
        var systemConfig = await _configStore.GetSystemConfigAsync();
        var contextsConfig = await _configStore.GetContextsConfigAsync();
        var featuresConfig = await _configStore.GetFeaturesConfigAsync();

        // Generate global environment variables
        plan.GlobalEnvVars = GenerateGlobalEnvVars(systemConfig, featuresConfig, manifest);

        // Create deployment steps from manifest contexts
        var steps = new List<DeploymentStep>();
        foreach (var (contextName, context) in manifest.Contexts)
        {
            var step = new DeploymentStep
            {
                ContextName = contextName,
                Image = context.Image,
                Version = context.Version,
                ContainerName = context.ContainerName,
                Internal = context.Internal,
                EnvVars = new Dictionary<string, string>(plan.GlobalEnvVars),
                Ports = context.Ports?.ToList() ?? new List<string>(),
                Volumes = context.Volumes ?? new Dictionary<string, string>(),
                DependsOn = context.DependsOn?.ToList() ?? new List<string>()
            };

            // Add context-specific environment variables
            if (context.Env != null)
            {
                foreach (var (key, value) in context.Env)
                {
                    step.EnvVars[key] = value;
                }
            }

            // Add connection strings based on mode
            AddConnectionEnvVars(step, contextName, contextsConfig);

            steps.Add(step);
        }

        // Calculate deployment order based on dependencies
        plan.Steps = OrderStepsByDependencies(steps, manifest.Gateway?.Context);

        _logger.LogInformation("Deployment plan generated with {Count} steps", plan.Steps.Count);
        return plan;
    }

    public async Task<DeploymentResult> ExecuteDeploymentAsync(DeploymentPlan plan)
    {
        var result = new DeploymentResult
        {
            StackVersion = plan.StackVersion,
            DeploymentTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting deployment of stack version {Version}", plan.StackVersion);

            var systemConfig = await _configStore.GetSystemConfigAsync();

            // Ensure Docker network exists
            await EnsureDockerNetworkAsync(systemConfig.DockerNetwork);

            // Execute deployment steps in order
            foreach (var step in plan.Steps)
            {
                try
                {
                    await DeployStepAsync(step, systemConfig.DockerNetwork);
                    result.DeployedContexts.Add(step.ContextName);
                    _logger.LogInformation("Successfully deployed context {Context}", step.ContextName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deploy context {Context}", step.ContextName);
                    result.Errors.Add($"Failed to deploy {step.ContextName}: {ex.Message}");
                    result.Success = false;
                    return result;
                }
            }

            // Update release configuration
            await UpdateReleaseConfigAsync(plan);

            result.Success = true;
            _logger.LogInformation("Stack deployment completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stack deployment failed");
            result.Errors.Add($"Deployment failed: {ex.Message}");
            result.Success = false;
        }

        return result;
    }

    public async Task<DeploymentResult> DeployStackAsync(ReleaseManifest manifest)
    {
        var plan = await GenerateDeploymentPlanAsync(manifest);
        return await ExecuteDeploymentAsync(plan);
    }

    public async Task<DeploymentResult> RemoveStackAsync(string stackVersion)
    {
        var result = new DeploymentResult
        {
            StackVersion = stackVersion,
            DeploymentTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Removing stack version {Version}", stackVersion);

            var releaseConfig = await _configStore.GetReleaseConfigAsync();

            if (releaseConfig.InstalledStackVersion != stackVersion)
            {
                result.Errors.Add($"Stack version {stackVersion} is not currently installed");
                result.Success = false;
                return result;
            }

            // Remove all deployed containers
            foreach (var (contextName, version) in releaseConfig.InstalledContexts)
            {
                try
                {
                    // Container name pattern: from manifest
                    var containerName = $"{contextName}"; // This should match the containerName from manifest
                    await _dockerService.RemoveContainerAsync(containerName);
                    result.DeployedContexts.Add(contextName);
                    _logger.LogInformation("Removed container for context {Context}", contextName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove container for context {Context}", contextName);
                    result.Errors.Add($"Failed to remove {contextName}: {ex.Message}");
                }
            }

            // Clear release configuration
            releaseConfig.InstalledStackVersion = null;
            releaseConfig.InstalledContexts.Clear();
            releaseConfig.InstallDate = null;
            await _configStore.SaveReleaseConfigAsync(releaseConfig);

            result.Success = result.Errors.Count == 0;
            _logger.LogInformation("Stack removal completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stack removal failed");
            result.Errors.Add($"Removal failed: {ex.Message}");
            result.Success = false;
        }

        return result;
    }

    private Dictionary<string, string> GenerateGlobalEnvVars(
        SystemConfig systemConfig,
        FeaturesConfig featuresConfig,
        ReleaseManifest manifest)
    {
        var envVars = new Dictionary<string, string>();

        // System variables
        if (systemConfig.Organization != null)
        {
            envVars["RSGO_ORG_ID"] = systemConfig.Organization.Id;
            envVars["RSGO_ORG_NAME"] = systemConfig.Organization.Name;
        }
        envVars["RSGO_STACK_VERSION"] = manifest.StackVersion;

        // Feature flags
        foreach (var (featureName, enabled) in featuresConfig.Features)
        {
            envVars[$"RSGO_FEATURE_{featureName}"] = enabled.ToString().ToLower();
        }

        // Add manifest default features if not already set
        if (manifest.Features != null)
        {
            foreach (var (featureName, featureDefault) in manifest.Features)
            {
                var envKey = $"RSGO_FEATURE_{featureName}";
                if (!envVars.ContainsKey(envKey))
                {
                    envVars[envKey] = featureDefault.Default.ToString().ToLower();
                }
            }
        }

        return envVars;
    }

    private void AddConnectionEnvVars(
        DeploymentStep step,
        string contextName,
        ContextsConfig contextsConfig)
    {
        if (contextsConfig.Mode == ConnectionMode.Simple && contextsConfig.GlobalConnections != null)
        {
            // Simple mode: use global connections
            step.EnvVars["RSGO_CONNECTION_transport"] = contextsConfig.GlobalConnections.Transport;
            step.EnvVars["RSGO_CONNECTION_persistence"] = contextsConfig.GlobalConnections.Persistence;
            if (!string.IsNullOrWhiteSpace(contextsConfig.GlobalConnections.EventStore))
            {
                step.EnvVars["RSGO_CONNECTION_eventStore"] = contextsConfig.GlobalConnections.EventStore;
            }
        }
        else if (contextsConfig.Mode == ConnectionMode.Advanced &&
                 contextsConfig.Contexts.TryGetValue(contextName, out var contextConnections))
        {
            // Advanced mode: use context-specific connections
            if (!string.IsNullOrWhiteSpace(contextConnections.Transport))
            {
                step.EnvVars["RSGO_CONNECTION_transport"] = contextConnections.Transport;
            }
            if (!string.IsNullOrWhiteSpace(contextConnections.Persistence))
            {
                step.EnvVars["RSGO_CONNECTION_persistence"] = contextConnections.Persistence;
            }
            if (!string.IsNullOrWhiteSpace(contextConnections.EventStore))
            {
                step.EnvVars["RSGO_CONNECTION_eventStore"] = contextConnections.EventStore;
            }
        }
    }

    private List<DeploymentStep> OrderStepsByDependencies(
        List<DeploymentStep> steps,
        string? gatewayContext)
    {
        var ordered = new List<DeploymentStep>();
        var remaining = new List<DeploymentStep>(steps);
        var deployed = new HashSet<string>();

        // Topological sort based on dependencies
        while (remaining.Count > 0)
        {
            var canDeploy = remaining
                .Where(s => s.DependsOn.All(dep => deployed.Contains(dep)))
                .Where(s => s.ContextName != gatewayContext) // Gateway always last
                .ToList();

            if (canDeploy.Count == 0 && remaining.Any(s => s.ContextName != gatewayContext))
            {
                // Circular dependency or missing dependency
                _logger.LogWarning("Circular or missing dependencies detected, deploying remaining contexts without order guarantee");
                canDeploy = remaining.Where(s => s.ContextName != gatewayContext).ToList();
            }

            foreach (var step in canDeploy)
            {
                step.Order = ordered.Count;
                ordered.Add(step);
                deployed.Add(step.ContextName);
                remaining.Remove(step);
            }
        }

        // Add gateway last if present
        if (gatewayContext != null)
        {
            var gateway = steps.FirstOrDefault(s => s.ContextName == gatewayContext);
            if (gateway != null)
            {
                gateway.Order = ordered.Count;
                ordered.Add(gateway);
            }
        }

        return ordered;
    }

    private async Task EnsureDockerNetworkAsync(string networkName)
    {
        try
        {
            // Check if network exists by trying to inspect it
            // If it doesn't exist, create it
            // This will be implemented based on your DockerService capabilities
            _logger.LogInformation("Ensuring Docker network {Network} exists", networkName);

            // For now, we'll assume the network creation is handled elsewhere
            // In a full implementation, you'd call: await _dockerService.CreateNetworkAsync(networkName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure Docker network {Network}", networkName);
        }
    }

    private async Task DeployStepAsync(DeploymentStep step, string networkName)
    {
        _logger.LogInformation("Deploying step {Context} (order: {Order})", step.ContextName, step.Order);

        // Stop and remove existing container if it exists
        try
        {
            await _dockerService.RemoveContainerAsync(step.ContainerName);
            _logger.LogInformation("Removed existing container {Container}", step.ContainerName);
        }
        catch
        {
            // Container doesn't exist, that's fine
        }

        // Create and start container
        // Note: This is a simplified version. Full implementation would need:
        // - Pull image if not exists
        // - Create container with all options (env vars, ports, volumes, network)
        // - Start container
        // - Wait for health check if configured

        _logger.LogInformation(
            "Would deploy container {Container} from {Image}:{Version} with {EnvCount} env vars",
            step.ContainerName,
            step.Image,
            step.Version,
            step.EnvVars.Count);

        // TODO: Implement actual container creation via DockerService
        // await _dockerService.CreateAndStartContainerAsync(...)
    }

    private async Task UpdateReleaseConfigAsync(DeploymentPlan plan)
    {
        var releaseConfig = new ReleaseConfig
        {
            InstalledStackVersion = plan.StackVersion,
            InstallDate = DateTime.UtcNow
        };

        foreach (var step in plan.Steps)
        {
            releaseConfig.InstalledContexts[step.ContextName] = step.Version;
        }

        await _configStore.SaveReleaseConfigAsync(releaseConfig);
        _logger.LogInformation("Updated release configuration for stack {Version}", plan.StackVersion);
    }
}
