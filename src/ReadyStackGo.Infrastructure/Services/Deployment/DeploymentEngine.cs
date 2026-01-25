using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Parsing;
using static ReadyStackGo.Infrastructure.Docker.DockerNamingUtility;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

namespace ReadyStackGo.Infrastructure.Services.Deployment;

/// <summary>
/// Deployment engine that orchestrates stack deployments based on manifests
/// Implements the deployment pipeline from specification chapter 8
/// v0.6: Uses SQLite repositories for Organization/Environment data
/// </summary>
public class DeploymentEngine : IDeploymentEngine
{
    private readonly IConfigStore _configStore;
    private readonly IDockerService _dockerService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly ILogger<DeploymentEngine> _logger;

    public DeploymentEngine(
        IConfigStore configStore,
        IDockerService dockerService,
        IOrganizationRepository organizationRepository,
        IEnvironmentRepository environmentRepository,
        ILogger<DeploymentEngine> logger)
    {
        _configStore = configStore;
        _dockerService = dockerService;
        _organizationRepository = organizationRepository;
        _environmentRepository = environmentRepository;
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
        var featuresConfig = await _configStore.GetFeaturesConfigAsync();

        // Get organization from SQLite
        var organization = _organizationRepository.GetAll().FirstOrDefault();

        // Generate global environment variables
        plan.GlobalEnvVars = GenerateGlobalEnvVars(organization, featuresConfig, manifest);

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

            steps.Add(step);
        }

        // Calculate deployment order based on dependencies
        plan.Steps = OrderStepsByDependencies(steps, manifest.Gateway?.Context);

        _logger.LogInformation("Deployment plan generated with {Count} steps", plan.Steps.Count);
        return plan;
    }

    public Task<DeploymentResult> ExecuteDeploymentAsync(DeploymentPlan plan)
    {
        return ExecuteDeploymentAsync(plan, null, CancellationToken.None);
    }

    public async Task<DeploymentResult> ExecuteDeploymentAsync(
        DeploymentPlan plan,
        DeploymentProgressCallback? progressCallback,
        CancellationToken cancellationToken = default)
    {
        var result = new DeploymentResult
        {
            StackVersion = plan.StackVersion,
            DeploymentTime = DateTime.UtcNow
        };

        // Separate init containers from regular services
        var initSteps = plan.Steps.Where(s => s.Lifecycle == ServiceLifecycle.Init).ToList();
        var regularSteps = plan.Steps.Where(s => s.Lifecycle == ServiceLifecycle.Service).ToList();

        var totalSteps = plan.Steps.Count;
        var completedSteps = 0;

        // Define deployment phases with their weights (end of last phase = 100)
        // Each phase reports its own 0-100% progress internally
        // Pulling images takes the most time (network download), starting containers is fast
        var phaseWeights = new Dictionary<string, (int Start, int End)>
        {
            ["Initializing"] = (0, 2),              // 0-2%
            ["Network"] = (2, 5),                   // 2-5%
            ["RemovingOldContainers"] = (5, 10),    // 5-10%
            ["PullingImages"] = (10, 70),           // 10-70% (60% - this is the slow part)
            ["InitializingContainers"] = (70, 80),  // 70-80% (10% - init containers)
            ["StartingServices"] = (80, 100),       // 80-100% (20% - regular services)
            ["Complete"] = (100, 100)               // 100% (instant)
        };

        // Helper to calculate overall progress from phase-local progress (0-100)
        int CalculateOverallProgress(string phase, int phaseProgress)
        {
            if (!phaseWeights.TryGetValue(phase, out var weight))
                return phaseProgress; // Unknown phase, use raw value

            var phaseRange = weight.End - weight.Start;
            return weight.Start + (phaseProgress * phaseRange / 100);
        }

        // Helper to report progress - phases report their own 0-100% progress
        async Task ReportProgress(string phase, string message, int phaseProgress = 0, string? currentService = null)
        {
            if (progressCallback != null)
            {
                var overallPercent = CalculateOverallProgress(phase, phaseProgress);
                await progressCallback(phase, message, overallPercent, currentService, totalSteps, completedSteps);
            }
        }

        try
        {
            _logger.LogInformation("Starting deployment of stack version {Version}", plan.StackVersion);
            await ReportProgress("Initializing", $"Starting deployment of {plan.StackName ?? plan.StackVersion}");

            // Determine environment ID - use from plan or fall back to default
            var environmentId = plan.EnvironmentId;
            if (string.IsNullOrEmpty(environmentId))
            {
                // Fall back to default environment from SQLite
                var organization = _organizationRepository.GetAll().FirstOrDefault();
                if (organization != null)
                {
                    // Convert IdentityAccess OrgId to Deployment OrgId
                    var deploymentOrgId = DeploymentOrganizationId.FromIdentityAccess(organization.Id);
                    var defaultEnv = _environmentRepository.GetDefault(deploymentOrgId);
                    environmentId = defaultEnv?.Id.ToString();
                }

                if (string.IsNullOrEmpty(environmentId))
                {
                    result.Errors.Add("No environment specified and no default environment configured");
                    result.Success = false;
                    return result;
                }
            }

            // Determine stack name first (needed for network naming)
            var stackName = plan.StackName ?? plan.StackVersion;

            // Create all non-external networks defined in the plan
            await ReportProgress("Network", "Creating Docker networks...");
            foreach (var (networkName, networkDef) in plan.Networks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!networkDef.External)
                {
                    // Create the network with resolved name (already prefixed with stack name)
                    await EnsureDockerNetworkAsync(environmentId, networkDef.ResolvedName);
                }
                else
                {
                    _logger.LogInformation("Network '{NetworkName}' is external, assuming it already exists",
                        networkDef.ResolvedName);
                }
            }

            // Fallback: if no networks defined, create a default stack network
            var defaultNetwork = CreateNetworkName(stackName, "default");
            if (plan.Networks.Count == 0)
            {
                await EnsureDockerNetworkAsync(environmentId, defaultNetwork);
            }

            // PHASE 1: Remove existing containers first (Point of No Return)
            // This ensures rollback is meaningful - if we remove containers, we need rollback capability
            await ReportProgress("RemovingOldContainers", $"Removing {plan.Steps.Count} existing containers...", 0);

            var removedContainers = 0;
            foreach (var step in plan.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var existing = await _dockerService.GetContainerByNameAsync(environmentId, step.ContainerName);
                    if (existing != null)
                    {
                        _logger.LogInformation("Removing existing container {Container} (Point of No Return)", step.ContainerName);
                        await _dockerService.RemoveContainerAsync(environmentId, existing.Id, force: true);
                        removedContainers++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "No existing container {Container} to remove", step.ContainerName);
                }

                var removePercent = plan.Steps.Count > 0 ? ((plan.Steps.IndexOf(step) + 1) * 100 / plan.Steps.Count) : 100;
                await ReportProgress("RemovingOldContainers", $"Removed {removedContainers} containers", removePercent, step.ContextName);
            }

            _logger.LogInformation("Removed {Count} existing containers - Point of No Return passed", removedContainers);

            // PHASE 2: Pull all images (fail here means rollback is needed)
            var pullWarnings = new Dictionary<string, string>();
            var pulledImages = 0;
            var totalImages = plan.Steps.Count;

            await ReportProgress("PullingImages", $"Pulling {totalImages} images...", 0);

            // Helper to report progress during pull phase - passes pulledImages as completedSteps
            async Task ReportPullProgress(string message, int phaseProgress, string? currentService = null)
            {
                if (progressCallback != null)
                {
                    var overallPercent = CalculateOverallProgress("PullingImages", phaseProgress);
                    await progressCallback("PullingImages", message, overallPercent, currentService, totalImages, pulledImages);
                }
            }

            foreach (var step in plan.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (imageName, imageTag) = ParseImageReference(step.Image, step.Version);
                var fullImageName = $"{imageName}:{imageTag}";

                // Phase-local progress: where we are before pulling this image
                var beforePullPercent = totalImages > 0 ? (pulledImages * 100 / totalImages) : 0;
                await ReportPullProgress($"Pulling {fullImageName}...", beforePullPercent, step.ContextName);

                try
                {
                    _logger.LogInformation("Pulling image {Image} for {Context}", fullImageName, step.ContextName);
                    await _dockerService.PullImageAsync(environmentId, imageName, imageTag);
                    _logger.LogInformation("Successfully pulled image {Image}", fullImageName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to pull image {Image}", fullImageName);

                    // Check if image exists locally as fallback
                    var imageExists = await _dockerService.ImageExistsAsync(environmentId, imageName, imageTag);
                    if (!imageExists)
                    {
                        var errorMessage = $"Service '{step.ContextName}': Failed to pull image '{imageName}' (tag: {imageTag}) - no local copy exists. " +
                            $"Please ensure the image exists and registry credentials are configured. Error: {ex.Message}";
                        _logger.LogError(errorMessage);
                        result.Errors.Add(errorMessage);
                        result.Success = false;
                        await ReportProgress("Failed", errorMessage, 0, step.ContextName);
                        return result;
                    }

                    pullWarnings[step.ContextName] = $"Image '{fullImageName}' could not be pulled - using existing local image. The deployed version may be outdated.";
                    _logger.LogWarning("Using existing local image {Image} (pull failed: {Error})", fullImageName, ex.Message);
                }

                pulledImages++;
                // Phase-local progress: 0-100% within this phase
                var afterPullPercent = totalImages > 0 ? (pulledImages * 100 / totalImages) : 100;
                await ReportPullProgress($"Pulled {pulledImages}/{totalImages} images", afterPullPercent, step.ContextName);
            }

            await ReportPullProgress($"All {totalImages} images ready", 100);

            // PHASE: Initialize containers (run-once init containers)
            if (initSteps.Count > 0)
            {
                _logger.LogInformation("Phase: Initializing {Count} init container(s) before starting services", initSteps.Count);
                await ReportProgress("InitializingContainers", $"Running {initSteps.Count} init container(s)...", 0);

                var completedInits = 0;
                foreach (var initStep in initSteps)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var beforeInitPercent = initSteps.Count > 0 ? (completedInits * 100 / initSteps.Count) : 0;
                        await ReportProgress("InitializingContainers",
                            $"Running init container {initStep.ContextName}...",
                            beforeInitPercent,
                            initStep.ContextName);

                        _logger.LogInformation("Starting init container {ContextName} and waiting for completion...", initStep.ContextName);
                        var containerInfo = await StartInitContainerAndWaitAsync(
                            environmentId,
                            initStep,
                            defaultNetwork,
                            stackName,
                            cancellationToken);

                        // Add any pull warnings for this step
                        if (pullWarnings.TryGetValue(initStep.ContextName, out var warning))
                        {
                            result.Warnings.Add(warning);
                        }

                        result.DeployedContexts.Add(initStep.ContextName);
                        result.DeployedContainers.Add(containerInfo);
                        completedSteps++;
                        completedInits++;

                        var afterInitPercent = initSteps.Count > 0 ? (completedInits * 100 / initSteps.Count) : 100;
                        await ReportProgress("InitializingContainers",
                            $"Completed {completedInits}/{initSteps.Count} init container(s)",
                            afterInitPercent,
                            initStep.ContextName);

                        _logger.LogInformation("Init container {ContextName} completed successfully", initStep.ContextName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Init container {ContextName} failed", initStep.ContextName);
                        result.Errors.Add($"Init container '{initStep.ContextName}' failed: {ex.Message}");
                        result.Success = false;
                        return result;
                    }
                }

                _logger.LogInformation("All {Count} init container(s) completed successfully", initSteps.Count);
            }
            else
            {
                _logger.LogInformation("No init containers to run, proceeding to start services");
            }

            // PHASE: Start regular services in dependency order
            await ReportProgress("StartingServices", $"Starting {regularSteps.Count} service(s)...", 0);

            var completedServices = 0;
            foreach (var step in regularSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Phase-local progress: where we are before starting this service
                    var beforeStartPercent = regularSteps.Count > 0 ? (completedServices * 100 / regularSteps.Count) : 0;
                    await ReportProgress("StartingServices", $"Starting {step.ContextName}...", beforeStartPercent, step.ContextName);

                    var containerInfo = await StartContainerAsync(environmentId, step, defaultNetwork, stackName);

                    // Add any pull warnings for this step
                    if (pullWarnings.TryGetValue(step.ContextName, out var warning))
                    {
                        result.Warnings.Add(warning);
                    }

                    result.DeployedContexts.Add(step.ContextName);
                    result.DeployedContainers.Add(containerInfo);
                    completedSteps++;
                    completedServices++;

                    // Phase-local progress: 0-100% within this phase
                    var afterStartPercent = regularSteps.Count > 0 ? (completedServices * 100 / regularSteps.Count) : 100;
                    await ReportProgress("StartingServices", $"Started {completedServices}/{regularSteps.Count} service(s)", afterStartPercent, step.ContextName);

                    _logger.LogInformation("Successfully started service {Context}", step.ContextName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start service {Context}", step.ContextName);
                    result.Errors.Add($"Failed to start service '{step.ContextName}': {ex.Message}");
                    result.Success = false;
                    return result;
                }
            }

            // Update release configuration (instant, no separate progress phase needed)
            await UpdateReleaseConfigAsync(plan);

            result.Success = true;
            _logger.LogInformation("Stack deployment completed successfully");

            // Report 100% completion
            await ReportProgress("Complete", $"Successfully deployed {stackName} with {totalSteps} services", 100);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Deployment was cancelled");
            result.Errors.Add("Deployment was cancelled");
            result.Success = false;
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

    public async Task<DeploymentResult> RemoveStackAsync(string environmentId, string stackVersion)
    {
        var result = new DeploymentResult
        {
            StackVersion = stackVersion,
            DeploymentTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Removing stack version {Version} from environment {EnvironmentId}",
                stackVersion, environmentId);

            // Get all containers with the rsgo.stack label matching stackVersion
            var containers = await _dockerService.ListContainersAsync(environmentId);
            var stackContainers = containers
                .Where(c => c.Labels.TryGetValue("rsgo.stack", out var stack) && stack == stackVersion)
                .ToList();

            if (!stackContainers.Any())
            {
                _logger.LogWarning("No containers found for stack {Version} in environment {EnvironmentId}",
                    stackVersion, environmentId);
            }

            // Remove all containers for this stack
            foreach (var container in stackContainers)
            {
                try
                {
                    _logger.LogInformation("Removing container {Name} ({Id})", container.Name, container.Id);
                    await _dockerService.RemoveContainerAsync(environmentId, container.Id, force: true);

                    // Extract context name from label
                    if (container.Labels.TryGetValue("rsgo.context", out var contextName))
                    {
                        result.DeployedContexts.Add(contextName);
                    }
                    else
                    {
                        result.DeployedContexts.Add(container.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove container {Name}", container.Name);
                    result.Errors.Add($"Failed to remove {container.Name}: {ex.Message}");
                }
            }

            // Clear release configuration
            var releaseConfig = await _configStore.GetReleaseConfigAsync();
            if (releaseConfig.InstalledStackVersion == stackVersion)
            {
                releaseConfig.InstalledStackVersion = null;
                releaseConfig.InstalledContexts.Clear();
                releaseConfig.InstallDate = null;
                await _configStore.SaveReleaseConfigAsync(releaseConfig);
            }

            result.Success = result.Errors.Count == 0;
            _logger.LogInformation("Stack removal completed for {Version}", stackVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stack removal failed");
            result.Errors.Add($"Removal failed: {ex.Message}");
            result.Success = false;
        }

        return result;
    }

    public async Task<DeploymentResult> RemoveStackAsync(string environmentId, string stackVersion, DeploymentProgressCallback? progressCallback)
    {
        var result = new DeploymentResult
        {
            StackVersion = stackVersion,
            DeploymentTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Removing stack version {Version} from environment {EnvironmentId} with progress tracking",
                stackVersion, environmentId);

            if (progressCallback != null)
            {
                await progressCallback("Initializing", "Finding containers to remove...", 5, null, 0, 0);
            }

            // Get all containers with the rsgo.stack label matching stackVersion
            var containers = await _dockerService.ListContainersAsync(environmentId);
            var stackContainers = containers
                .Where(c => c.Labels.TryGetValue("rsgo.stack", out var stack) && stack == stackVersion)
                .ToList();

            var totalContainers = stackContainers.Count;
            var removedCount = 0;

            if (!stackContainers.Any())
            {
                _logger.LogWarning("No containers found for stack {Version} in environment {EnvironmentId}",
                    stackVersion, environmentId);
                if (progressCallback != null)
                {
                    await progressCallback("Complete", "No containers to remove", 100, null, 0, 0);
                }
            }
            else
            {
                if (progressCallback != null)
                {
                    await progressCallback("RemovingContainers", $"Found {totalContainers} container(s) to remove", 10, null, totalContainers, 0);
                }

                // Remove all containers for this stack
                foreach (var container in stackContainers)
                {
                    try
                    {
                        var containerName = container.Labels.TryGetValue("rsgo.context", out var ctx) ? ctx : container.Name;

                        if (progressCallback != null)
                        {
                            var progressPercent = 10 + (int)((removedCount / (double)totalContainers) * 80);
                            await progressCallback("RemovingContainers", $"Removing {containerName}...", progressPercent, containerName, totalContainers, removedCount);
                        }

                        _logger.LogInformation("Removing container {Name} ({Id})", container.Name, container.Id);
                        await _dockerService.RemoveContainerAsync(environmentId, container.Id, force: true);

                        removedCount++;

                        // Extract context name from label
                        if (container.Labels.TryGetValue("rsgo.context", out var contextName))
                        {
                            result.DeployedContexts.Add(contextName);
                        }
                        else
                        {
                            result.DeployedContexts.Add(container.Name);
                        }

                        if (progressCallback != null)
                        {
                            var progressPercent = 10 + (int)((removedCount / (double)totalContainers) * 80);
                            await progressCallback("RemovingContainers", $"Removed {containerName}", progressPercent, null, totalContainers, removedCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove container {Name}", container.Name);
                        result.Errors.Add($"Failed to remove {container.Name}: {ex.Message}");
                    }
                }
            }

            if (progressCallback != null)
            {
                await progressCallback("Cleanup", "Cleaning up configuration...", 95, null, totalContainers, removedCount);
            }

            // Clear release configuration
            var releaseConfig = await _configStore.GetReleaseConfigAsync();
            if (releaseConfig.InstalledStackVersion == stackVersion)
            {
                releaseConfig.InstalledStackVersion = null;
                releaseConfig.InstalledContexts.Clear();
                releaseConfig.InstallDate = null;
                await _configStore.SaveReleaseConfigAsync(releaseConfig);
            }

            result.Success = result.Errors.Count == 0;
            _logger.LogInformation("Stack removal completed for {Version}", stackVersion);

            if (progressCallback != null)
            {
                await progressCallback("Complete", $"Successfully removed {removedCount} container(s)", 100, null, totalContainers, removedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stack removal failed");
            result.Errors.Add($"Removal failed: {ex.Message}");
            result.Success = false;

            if (progressCallback != null)
            {
                await progressCallback("Error", $"Removal failed: {ex.Message}", 100, null, 0, 0);
            }
        }

        return result;
    }

    private Dictionary<string, string> GenerateGlobalEnvVars(
        Domain.IdentityAccess.Organizations.Organization? organization,
        FeaturesConfig featuresConfig,
        ReleaseManifest manifest)
    {
        var envVars = new Dictionary<string, string>();

        // System variables from SQLite organization
        if (organization != null)
        {
            envVars["RSGO_ORG_ID"] = organization.Id.ToString();
            envVars["RSGO_ORG_NAME"] = organization.Name;
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

    private async Task EnsureDockerNetworkAsync(string environmentId, string networkName)
    {
        try
        {
            _logger.LogInformation("Ensuring Docker network {Network} exists in environment {EnvironmentId}",
                networkName, environmentId);

            await _dockerService.EnsureNetworkAsync(environmentId, networkName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure Docker network {Network} in environment {EnvironmentId}",
                networkName, environmentId);
        }
    }

    /// <summary>
    /// Starts a container for a deployment step (assumes image is already pulled and old container removed).
    /// Returns the container info for tracking.
    /// </summary>
    private async Task<DeployedContainerInfo> StartContainerAsync(
        string environmentId,
        DeploymentStep step,
        string defaultNetwork,
        string stackName)
    {
        _logger.LogInformation("Starting container for {Context} (order: {Order}) in environment {EnvironmentId}",
            step.ContextName, step.Order, environmentId);

        // Note: Old containers are removed in Phase 1 (RemovingOldContainers) before image pull
        // This ensures Point of No Return semantics - rollback is needed if anything fails after removal

        var (imageName, imageTag) = ParseImageReference(step.Image, step.Version);

        // Determine networks for this container
        var networks = step.Networks.Count > 0 ? step.Networks : new List<string> { defaultNetwork };

        _logger.LogDebug("Container {Container} will be connected to networks: {Networks}",
            step.ContainerName, string.Join(", ", networks));

        // Create and start container
        // Init containers use "on-failure" restart policy, regular services use "unless-stopped"
        var restartPolicy = step.Lifecycle == ServiceLifecycle.Init ? "on-failure" : "unless-stopped";

        var request = new CreateContainerRequest
        {
            Name = step.ContainerName,
            Image = $"{imageName}:{imageTag}",
            EnvironmentVariables = step.EnvVars,
            Ports = step.Ports,
            Volumes = step.Volumes,
            Networks = networks,
            NetworkAliases = new List<string> { step.ContextName },
            Labels = new Dictionary<string, string>
            {
                ["rsgo.stack"] = stackName,
                ["rsgo.context"] = step.ContextName,
                ["rsgo.environment"] = environmentId,
                ["rsgo.lifecycle"] = step.Lifecycle.ToString().ToLowerInvariant()
            },
            RestartPolicy = restartPolicy
        };

        var containerId = await _dockerService.CreateAndStartContainerAsync(environmentId, request);

        _logger.LogInformation(
            "Started container {Container} ({ContainerId}) from {Image}:{Tag} with {EnvCount} env vars on {NetworkCount} network(s)",
            step.ContainerName,
            containerId,
            imageName,
            imageTag,
            step.EnvVars.Count,
            networks.Count);

        return new DeployedContainerInfo
        {
            ServiceName = step.ContextName,
            ContainerId = containerId,
            ContainerName = step.ContainerName,
            Image = $"{imageName}:{imageTag}",
            Status = "running"
        };
    }

    /// <summary>
    /// Starts an init container and waits for it to complete successfully (exit code 0).
    /// Init containers are run-once containers like database migrators that must complete before regular services start.
    /// </summary>
    private async Task<DeployedContainerInfo> StartInitContainerAndWaitAsync(
        string environmentId,
        DeploymentStep step,
        string defaultNetwork,
        string stackName,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting init container {ContextName}...", step.ContextName);

        // Start the init container (uses restart: on-failure)
        var containerInfo = await StartContainerAsync(environmentId, step, defaultNetwork, stackName);

        // Wait for the container to complete
        _logger.LogInformation("Waiting for init container {ContextName} to complete...", step.ContextName);

        var maxWaitSeconds = 300; // 5 minutes timeout for init containers
        var pollIntervalMs = 500; // Check every 500ms
        var elapsed = 0;

        while (elapsed < maxWaitSeconds * 1000)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var container = await _dockerService.GetContainerByNameAsync(environmentId, step.ContainerName);
            if (container == null)
            {
                throw new InvalidOperationException($"Init container {step.ContainerName} disappeared during execution");
            }

            // Check if container has exited
            if (container.Status.StartsWith("exited", StringComparison.OrdinalIgnoreCase))
            {
                // Container has stopped - check exit code
                var exitCode = await _dockerService.GetContainerExitCodeAsync(environmentId, container.Id);

                if (exitCode == 0)
                {
                    _logger.LogInformation("Init container {ContextName} completed successfully (exit code 0)", step.ContextName);
                    containerInfo.Status = "completed";
                    return containerInfo;
                }
                else
                {
                    // Init container failed - get logs for diagnosis
                    var logs = await _dockerService.GetContainerLogsAsync(environmentId, container.Id, tail: 50);
                    _logger.LogError("Init container {ContextName} failed with exit code {ExitCode}", step.ContextName, exitCode);

                    throw new InvalidOperationException(
                        $"Init container '{step.ContextName}' failed with exit code {exitCode}. " +
                        $"Last 50 log lines:\n{logs}");
                }
            }

            // Container still running - wait and check again
            await Task.Delay(pollIntervalMs, cancellationToken);
            elapsed += pollIntervalMs;
        }

        // Timeout reached
        var timeoutLogs = await _dockerService.GetContainerLogsAsync(environmentId, containerInfo.ContainerId, tail: 50);
        throw new TimeoutException(
            $"Init container '{step.ContextName}' did not complete within {maxWaitSeconds} seconds. " +
            $"Last 50 log lines:\n{timeoutLogs}");
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

    /// <summary>
    /// Parse a Docker image reference into name and tag.
    /// Handles formats like:
    /// - nginx:latest -> (nginx, latest)
    /// - nginx -> (nginx, defaultTag)
    /// - registry.example.com/myimage:v1 -> (registry.example.com/myimage, v1)
    /// - registry.example.com:5000/myimage:v1 -> (registry.example.com:5000/myimage, v1)
    /// - registry.example.com:5000/myimage -> (registry.example.com:5000/myimage, defaultTag)
    /// </summary>
    private static (string Name, string Tag) ParseImageReference(string image, string defaultTag)
    {
        if (string.IsNullOrEmpty(image))
        {
            return (image, defaultTag);
        }

        // Find the last colon that could be a tag separator
        // A tag separator colon must:
        // 1. Be after any slash (so registry ports like registry.com:5000/image are not confused)
        // 2. Not have a slash after it
        var lastSlash = image.LastIndexOf('/');
        var lastColon = image.LastIndexOf(':');

        // If there's a colon after the last slash (or no slash), it's a tag separator
        if (lastColon > lastSlash && lastColon < image.Length - 1)
        {
            var name = image[..lastColon];
            var tag = image[(lastColon + 1)..];
            return (name, tag);
        }

        // No tag found, use default
        return (image, defaultTag);
    }
}
