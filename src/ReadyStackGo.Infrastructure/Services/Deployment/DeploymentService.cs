using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Users;
using StackManagement = ReadyStackGo.Domain.StackManagement.Stacks;
using DeploymentUserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.Infrastructure.Services.Deployment;

/// <summary>
/// Service for managing stack deployments.
/// Uses RSGo Manifest format exclusively.
/// v0.6: Fully migrated to SQLite persistence.
/// v0.12: RSGo manifest only (Docker Compose import via separate converter).
/// </summary>
public class DeploymentService : IDeploymentService
{
    private readonly IRsgoManifestParser _manifestParser;
    private readonly IDeploymentEngine _deploymentEngine;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DeploymentService> _logger;

    public DeploymentService(
        IRsgoManifestParser manifestParser,
        IDeploymentEngine deploymentEngine,
        IDeploymentRepository deploymentRepository,
        IEnvironmentRepository environmentRepository,
        IUserRepository userRepository,
        ILogger<DeploymentService> logger)
    {
        _manifestParser = manifestParser;
        _deploymentEngine = deploymentEngine;
        _deploymentRepository = deploymentRepository;
        _environmentRepository = environmentRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ParseComposeResponse> ParseComposeAsync(ParseComposeRequest request)
    {
        try
        {
            _logger.LogInformation("Parsing RSGo manifest");

            // Validate manifest
            var validation = await _manifestParser.ValidateAsync(request.YamlContent);
            if (!validation.IsValid)
            {
                return new ParseComposeResponse
                {
                    Success = false,
                    Message = "Manifest validation failed",
                    Errors = validation.Errors,
                    Warnings = validation.Warnings
                };
            }

            // Parse manifest once
            var manifest = await _manifestParser.ParseAsync(request.YamlContent);
            var variables = await _manifestParser.ExtractVariablesAsync(manifest);

            return new ParseComposeResponse
            {
                Success = true,
                Message = $"Successfully parsed RSGo manifest with {manifest.Services?.Count ?? 0} services",
                Services = manifest.Services?.Keys.ToList() ?? new List<string>(),
                Variables = variables.Select(v => new EnvironmentVariableInfo
                {
                    Name = v.Name,
                    DefaultValue = v.DefaultValue,
                    IsRequired = v.IsRequired,
                    UsedInServices = new List<string>()
                }).ToList(),
                Warnings = validation.Warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse manifest");
            return new ParseComposeResponse
            {
                Success = false,
                Message = $"Failed to parse manifest: {ex.Message}",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public Task<DeployComposeResponse> DeployComposeAsync(string environmentId, DeployComposeRequest request)
    {
        return DeployComposeAsync(environmentId, request, null, CancellationToken.None);
    }

    public async Task<DeployComposeResponse> DeployComposeAsync(
        string environmentId,
        DeployComposeRequest request,
        DeploymentServiceProgressCallback? progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deploying YAML stack {StackName} to environment {EnvironmentId}",
                request.StackName, environmentId);

            // Validate environment exists
            if (!Guid.TryParse(environmentId, out var envGuid))
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Invalid environment ID",
                    Errors = new List<string> { "Invalid environment ID format" }
                };
            }

            var environment = _environmentRepository.Get(new EnvironmentId(envGuid));
            if (environment == null)
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = $"Environment '{environmentId}' not found",
                    Errors = new List<string> { $"Environment '{environmentId}' not found" }
                };
            }

            // Report initial progress
            if (progressCallback != null)
            {
                await progressCallback("Validating", "Validating manifest...", 5, null, 0, 0, 0, 0);
            }

            // Validate manifest
            var validation = await _manifestParser.ValidateAsync(request.YamlContent);
            if (!validation.IsValid)
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Manifest validation failed",
                    Errors = validation.Errors
                };
            }

            // Report progress
            if (progressCallback != null)
            {
                await progressCallback("Parsing", "Parsing RSGo manifest...", 10, null, 0, 0, 0, 0);
            }

            // Parse manifest once
            var manifest = await _manifestParser.ParseAsync(request.YamlContent);

            if (progressCallback != null)
            {
                await progressCallback("Planning", "Creating deployment plan...", 15, null, 0, 0, 0, 0);
            }

            // Create deployment plan
            var plan = await _manifestParser.ConvertToDeploymentPlanAsync(
                manifest,
                request.Variables,
                request.StackName);
            plan.StackVersion = request.StackVersion ?? manifest.Metadata?.ProductVersion ?? "unspecified";

            // Set environment ID for the deployment
            plan.EnvironmentId = environmentId;
            plan.StackName = request.StackName;

            // Create progress adapter to convert DeploymentEngine callback to our callback
            DeploymentProgressCallback? engineCallback = null;
            if (progressCallback != null)
            {
                engineCallback = async (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
                {
                    // Scale the engine progress (0-100) to our range (20-90)
                    var scaledPercent = 20 + (percent * 70 / 100);
                    await progressCallback(phase, message, scaledPercent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers);
                };
            }

            // Execute deployment with progress callback
            var result = await _deploymentEngine.ExecuteDeploymentAsync(plan, engineCallback, cancellationToken);

            if (!result.Success)
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Deployment failed",
                    Errors = result.Errors
                };
            }

            // Report progress
            if (progressCallback != null)
            {
                await progressCallback("Persisting", "Saving deployment record...", 95, null, result.DeployedContexts.Count, result.DeployedContexts.Count, 0, 0);
            }

            // Get current user (use first admin for now - TODO: get from authentication context)
            var currentUser = _userRepository.GetAll().FirstOrDefault();
            // Convert IdentityAccess UserId to Deployment UserId
            var deploymentUserId = currentUser != null
                ? DeploymentUserId.FromIdentityAccess(currentUser.Id)
                : DeploymentUserId.NewId();

            // Create and persist deployment record for YAML-based deployment
            var deploymentId = _deploymentRepository.NextIdentity();
            var deployment = Domain.Deployment.Deployments.Deployment.StartInstallation(
                deploymentId,
                new EnvironmentId(envGuid),
                request.StackName, // YAML-based deployments use StackName as StackId
                request.StackName,
                request.StackName, // Use stack name as project name
                deploymentUserId);

            deployment.SetStackVersion(plan.StackVersion);

            // Store deployment variables for later use (e.g., maintenance observer)
            if (request.Variables != null && request.Variables.Count > 0)
            {
                deployment.SetVariables(request.Variables);
            }

            // Add services from deployment result
            foreach (var container in result.DeployedContainers)
            {
                deployment.AddService(container.ServiceName, container.Image, "starting");
                deployment.SetServiceContainerInfo(container.ServiceName, container.ContainerId, container.ContainerName, container.Status);
            }

            deployment.MarkAsRunning();

            _deploymentRepository.Add(deployment);
            _deploymentRepository.SaveChanges();

            _logger.LogInformation("Successfully deployed stack {StackName} with deployment ID {DeploymentId}",
                request.StackName, deploymentId);

            // Build success message with warning hint
            var message = $"Successfully deployed {request.StackName}";
            if (result.Warnings.Count > 0)
            {
                message += $" (with {result.Warnings.Count} warning{(result.Warnings.Count > 1 ? "s" : "")})";
            }

            // Report completion
            if (progressCallback != null)
            {
                await progressCallback("Complete", message, 100, null, result.DeployedContexts.Count, result.DeployedContexts.Count, 0, 0);
            }

            return new DeployComposeResponse
            {
                Success = true,
                Message = message,
                DeploymentId = deploymentId.ToString(),
                StackName = request.StackName,
                Services = result.DeployedContexts.Select(c => new DeployedServiceInfo
                {
                    ServiceName = c,
                    Status = "running"
                }).ToList(),
                Warnings = result.Warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy stack {StackName}", request.StackName);
            return new DeployComposeResponse
            {
                Success = false,
                Message = $"Deployment failed: {ex.Message}",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public Task<GetDeploymentResponse> GetDeploymentAsync(string environmentId, string stackName)
    {
        try
        {
            _logger.LogInformation("Getting deployment {StackName} in environment {EnvironmentId}",
                stackName, environmentId);

            if (!Guid.TryParse(environmentId, out var envGuid))
            {
                return Task.FromResult(new GetDeploymentResponse
                {
                    Success = false,
                    Message = "Invalid environment ID"
                });
            }

            var deployment = _deploymentRepository.GetByStackName(new EnvironmentId(envGuid), stackName);

            if (deployment == null)
            {
                return Task.FromResult(new GetDeploymentResponse
                {
                    Success = false,
                    Message = $"Deployment '{stackName}' not found in environment '{environmentId}'"
                });
            }

            return Task.FromResult(new GetDeploymentResponse
            {
                Success = true,
                StackName = deployment.StackName,
                StackVersion = deployment.StackVersion,
                DeploymentId = deployment.Id.ToString(),
                EnvironmentId = environmentId,
                DeployedAt = deployment.CreatedAt,
                Status = deployment.Status.ToString(),
                OperationMode = deployment.OperationMode.Name,
                Services = deployment.Services.Select(s => new DeployedServiceInfo
                {
                    ServiceName = s.ServiceName,
                    ContainerId = s.ContainerId,
                    Status = s.Status
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get deployment {StackName}", stackName);
            return Task.FromResult(new GetDeploymentResponse
            {
                Success = false,
                Message = $"Failed to get deployment: {ex.Message}"
            });
        }
    }

    public Task<GetDeploymentResponse> GetDeploymentByIdAsync(string environmentId, string deploymentId)
    {
        try
        {
            _logger.LogInformation("Getting deployment {DeploymentId} in environment {EnvironmentId}",
                deploymentId, environmentId);

            if (!Guid.TryParse(environmentId, out var envGuid))
            {
                return Task.FromResult(new GetDeploymentResponse
                {
                    Success = false,
                    Message = "Invalid environment ID"
                });
            }

            if (!Guid.TryParse(deploymentId, out var depGuid))
            {
                return Task.FromResult(new GetDeploymentResponse
                {
                    Success = false,
                    Message = "Invalid deployment ID"
                });
            }

            var deployment = _deploymentRepository.Get(new DeploymentId(depGuid));

            if (deployment == null || deployment.EnvironmentId != new EnvironmentId(envGuid))
            {
                return Task.FromResult(new GetDeploymentResponse
                {
                    Success = false,
                    Message = $"Deployment '{deploymentId}' not found in environment '{environmentId}'"
                });
            }

            return Task.FromResult(new GetDeploymentResponse
            {
                Success = true,
                StackName = deployment.StackName,
                StackVersion = deployment.StackVersion,
                DeploymentId = deployment.Id.ToString(),
                EnvironmentId = environmentId,
                DeployedAt = deployment.CreatedAt,
                Status = deployment.Status.ToString(),
                OperationMode = deployment.OperationMode.Name,
                Services = deployment.Services.Select(s => new DeployedServiceInfo
                {
                    ServiceName = s.ServiceName,
                    ContainerId = s.ContainerId,
                    Status = s.Status
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get deployment {DeploymentId}", deploymentId);
            return Task.FromResult(new GetDeploymentResponse
            {
                Success = false,
                Message = $"Failed to get deployment: {ex.Message}"
            });
        }
    }

    public Task<ListDeploymentsResponse> ListDeploymentsAsync(string environmentId)
    {
        try
        {
            _logger.LogInformation("Listing deployments in environment {EnvironmentId}", environmentId);

            if (!Guid.TryParse(environmentId, out var envGuid))
            {
                // Return empty list with success=true for invalid IDs (backward compatibility)
                return Task.FromResult(new ListDeploymentsResponse
                {
                    Success = true,
                    Deployments = new List<DeploymentSummary>()
                });
            }

            var deployments = _deploymentRepository.GetByEnvironment(new EnvironmentId(envGuid))
                .Where(d => d.Status != Domain.Deployment.Deployments.DeploymentStatus.Removed);

            return Task.FromResult(new ListDeploymentsResponse
            {
                Success = true,
                Deployments = deployments.Select(d => new DeploymentSummary
                {
                    StackName = d.StackName,
                    StackVersion = d.StackVersion,
                    DeploymentId = d.Id.ToString(),
                    DeployedAt = d.CreatedAt,
                    Status = d.Status.ToString(),
                    OperationMode = d.OperationMode.Name,
                    ServiceCount = d.Services.Count
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list deployments");
            return Task.FromResult(new ListDeploymentsResponse
            {
                Success = false,
                Deployments = new List<DeploymentSummary>()
            });
        }
    }

    public async Task<DeployComposeResponse> RemoveDeploymentAsync(string environmentId, string stackName)
    {
        try
        {
            _logger.LogInformation("Removing deployment {StackName} from environment {EnvironmentId}",
                stackName, environmentId);

            if (!Guid.TryParse(environmentId, out var envGuid))
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Invalid environment ID",
                    Errors = new List<string> { "Invalid environment ID format" }
                };
            }

            // Remove the stack using the deployment engine
            var result = await _deploymentEngine.RemoveStackAsync(environmentId, stackName);

            if (!result.Success)
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Failed to remove deployment",
                    Errors = result.Errors
                };
            }

            // Remove deployment record from database (if not already removed)
            var deployment = _deploymentRepository.GetByStackName(new EnvironmentId(envGuid), stackName);
            if (deployment != null)
            {
                _logger.LogInformation("Found deployment {DeploymentId} with status {Status} for stack {StackName}",
                    deployment.Id, deployment.Status, stackName);

                if (deployment.Status != Domain.Deployment.Deployments.DeploymentStatus.Removed)
                {
                    deployment.MarkAsRemoved();
                    // Note: Don't call Update() - entity is already tracked from GetByStackName() query
                    _deploymentRepository.SaveChanges();
                    _logger.LogInformation("Marked deployment {DeploymentId} as removed", deployment.Id);
                }
            }
            else
            {
                _logger.LogWarning("No deployment found for stack {StackName} in environment {EnvironmentId}",
                    stackName, environmentId);
            }

            return new DeployComposeResponse
            {
                Success = true,
                Message = $"Successfully removed {stackName}",
                StackName = stackName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove deployment {StackName}", stackName);
            return new DeployComposeResponse
            {
                Success = false,
                Message = $"Failed to remove deployment: {ex.Message}",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<DeployComposeResponse> RemoveDeploymentByIdAsync(string environmentId, string deploymentId)
    {
        try
        {
            _logger.LogInformation("Removing deployment {DeploymentId} from environment {EnvironmentId}",
                deploymentId, environmentId);

            if (!Guid.TryParse(environmentId, out var envGuid))
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Invalid environment ID",
                    Errors = new List<string> { "Invalid environment ID format" }
                };
            }

            if (!Guid.TryParse(deploymentId, out var depGuid))
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Invalid deployment ID",
                    Errors = new List<string> { "Invalid deployment ID format" }
                };
            }

            // Find the deployment first to get its stack name
            var deployment = _deploymentRepository.Get(new DeploymentId(depGuid));
            if (deployment == null || deployment.EnvironmentId != new EnvironmentId(envGuid))
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = $"Deployment '{deploymentId}' not found in environment '{environmentId}'",
                    Errors = new List<string> { "Deployment not found" }
                };
            }

            var stackName = deployment.StackName;

            // Remove the stack using the deployment engine
            var result = await _deploymentEngine.RemoveStackAsync(environmentId, stackName);

            if (!result.Success)
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Failed to remove deployment",
                    Errors = result.Errors
                };
            }

            // Mark deployment as removed in database
            if (deployment.Status != Domain.Deployment.Deployments.DeploymentStatus.Removed)
            {
                deployment.MarkAsRemoved();
                // Note: Don't call Update() - entity is already tracked from Get() query
                _deploymentRepository.SaveChanges();
                _logger.LogInformation("Marked deployment {DeploymentId} as removed", deployment.Id);
            }

            return new DeployComposeResponse
            {
                Success = true,
                Message = $"Successfully removed {stackName}",
                StackName = stackName,
                DeploymentId = deploymentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove deployment {DeploymentId}", deploymentId);
            return new DeployComposeResponse
            {
                Success = false,
                Message = $"Failed to remove deployment: {ex.Message}",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<DeployComposeResponse> RemoveDeploymentByIdAsync(
        string environmentId,
        string deploymentId,
        DeploymentServiceProgressCallback? progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Removing deployment {DeploymentId} from environment {EnvironmentId} with progress tracking",
                deploymentId, environmentId);

            // Report initial progress
            if (progressCallback != null)
            {
                await progressCallback("Initializing", "Preparing to remove deployment...", 0, null, 0, 0, 0, 0);
            }

            if (!Guid.TryParse(environmentId, out var envGuid))
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Invalid environment ID",
                    Errors = new List<string> { "Invalid environment ID format" }
                };
            }

            if (!Guid.TryParse(deploymentId, out var depGuid))
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Invalid deployment ID",
                    Errors = new List<string> { "Invalid deployment ID format" }
                };
            }

            // Find the deployment first to get its stack name
            var deployment = _deploymentRepository.Get(new DeploymentId(depGuid));
            if (deployment == null || deployment.EnvironmentId != new EnvironmentId(envGuid))
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = $"Deployment '{deploymentId}' not found in environment '{environmentId}'",
                    Errors = new List<string> { "Deployment not found" }
                };
            }

            var stackName = deployment.StackName;
            var totalServices = deployment.Services.Count;

            if (progressCallback != null)
            {
                await progressCallback("RemovingContainers", $"Removing {stackName}...", 10, null, totalServices, 0, 0, 0);
            }

            // Convert DeploymentServiceProgressCallback to DeploymentProgressCallback for the engine
            DeploymentProgressCallback? engineCallback = null;
            if (progressCallback != null)
            {
                engineCallback = async (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
                {
                    await progressCallback(phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers);
                };
            }

            // Remove the stack using the deployment engine with progress
            var result = await _deploymentEngine.RemoveStackAsync(environmentId, stackName, engineCallback);

            if (!result.Success)
            {
                if (progressCallback != null)
                {
                    await progressCallback("Error", "Removal failed", 100, null, totalServices, 0, 0, 0);
                }
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Failed to remove deployment",
                    Errors = result.Errors
                };
            }

            // Mark deployment as removed in database
            if (deployment.Status != Domain.Deployment.Deployments.DeploymentStatus.Removed)
            {
                deployment.MarkAsRemoved();
                _deploymentRepository.SaveChanges();
                _logger.LogInformation("Marked deployment {DeploymentId} as removed", deployment.Id);
            }

            if (progressCallback != null)
            {
                await progressCallback("Complete", $"Successfully removed {stackName}", 100, null, totalServices, totalServices, 0, 0);
            }

            return new DeployComposeResponse
            {
                Success = true,
                Message = $"Successfully removed {stackName}",
                StackName = stackName,
                DeploymentId = deploymentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove deployment {DeploymentId}", deploymentId);
            if (progressCallback != null)
            {
                await progressCallback("Error", $"Failed: {ex.Message}", 100, null, 0, 0, 0, 0);
            }
            return new DeployComposeResponse
            {
                Success = false,
                Message = $"Failed to remove deployment: {ex.Message}",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<DeployStackResponse> DeployStackAsync(
        string? environmentId,
        DeployStackRequest request,
        DeploymentServiceProgressCallback? progressCallback,
        CancellationToken cancellationToken = default)
    {
        // Track upgrade state for exception handling
        bool isUpgrade = false;
        Domain.Deployment.Deployments.Deployment? existingDeployment = null;

        try
        {
            _logger.LogInformation("Deploying stack {StackName} to environment {EnvironmentId} (catalog: {CatalogStackId})",
                request.StackName, environmentId, request.CatalogStackId);

            // Validate environment exists
            if (string.IsNullOrEmpty(environmentId) || !Guid.TryParse(environmentId, out var envGuid))
            {
                return DeployStackResponse.Failed("Invalid environment ID", "Invalid environment ID format");
            }

            var environment = _environmentRepository.Get(new EnvironmentId(envGuid));
            if (environment == null)
            {
                return DeployStackResponse.Failed(
                    $"Environment '{environmentId}' not found",
                    $"Environment '{environmentId}' not found");
            }

            // Report initial progress
            if (progressCallback != null)
            {
                await progressCallback("Validating", "Validating services...", 5, null, 0, 0, 0, 0);
            }

            // Validate we have services to deploy
            if (request.Services == null || request.Services.Count == 0)
            {
                return DeployStackResponse.Failed("No services to deploy", "Stack has no services defined");
            }

            // Report progress
            if (progressCallback != null)
            {
                await progressCallback("Planning", "Creating deployment plan...", 15, null, 0, 0, 0, 0);
            }

            // Create deployment plan directly from structured data (no YAML parsing needed)
            var plan = CreateDeploymentPlanFromStructuredData(request);
            plan.EnvironmentId = environmentId;
            plan.StackName = request.StackName;

            // Get current user
            var currentUser = _userRepository.GetAll().FirstOrDefault();
            var deploymentUserId = currentUser != null
                ? DeploymentUserId.FromIdentityAccess(currentUser.Id)
                : DeploymentUserId.NewId();

            // Check for existing deployment (upgrade scenario)
            existingDeployment = _deploymentRepository.GetByStackName(
                new EnvironmentId(envGuid), request.StackName);

            // Check if this is an upgrade (existing running deployment) or a retry after failure
            isUpgrade = existingDeployment != null &&
                (existingDeployment.Status == Domain.Deployment.Deployments.DeploymentStatus.Running ||
                 existingDeployment.Status == Domain.Deployment.Deployments.DeploymentStatus.Failed) &&
                !string.IsNullOrEmpty(existingDeployment.StackVersion);

            string? previousVersion = null;

            if (isUpgrade)
            {
                previousVersion = existingDeployment!.StackVersion;
                _logger.LogInformation("Upgrading deployment {DeploymentId} from {OldVersion} to {NewVersion}",
                    existingDeployment.Id, previousVersion, plan.StackVersion);

                // Transition deployment to Upgrading status before executing
                // This is required so MarkAsFailed can work if deployment fails
                if (existingDeployment.Status == Domain.Deployment.Deployments.DeploymentStatus.Running)
                {
                    existingDeployment.StartUpgradeProcess(plan.StackVersion ?? "unknown");
                }
                else if (existingDeployment.Status == Domain.Deployment.Deployments.DeploymentStatus.Failed)
                {
                    existingDeployment.StartRollbackProcess(plan.StackVersion ?? "unknown");
                }
                _deploymentRepository.SaveChanges();
            }

            // Create progress adapter to convert DeploymentEngine callback to our callback
            DeploymentProgressCallback? engineCallback = null;
            if (progressCallback != null)
            {
                engineCallback = async (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
                {
                    // Scale the engine progress (0-100) to our range (20-90)
                    var scaledPercent = 20 + (percent * 70 / 100);
                    await progressCallback(phase, message, scaledPercent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers);
                };
            }

            // Execute deployment with progress callback
            // For upgrades: old containers are removed and new ones created here
            var result = await _deploymentEngine.ExecuteDeploymentAsync(plan, engineCallback, cancellationToken);

            if (!result.Success)
            {
                // Deployment failed
                if (isUpgrade && existingDeployment != null)
                {
                    // Mark all services as removed (containers were deleted)
                    existingDeployment.MarkAllServicesAsRemoved();

                    // Mark deployment as Failed - can be redeployed using existing version/variables
                    existingDeployment.MarkAsFailed(string.Join("; ", result.Errors));
                    _deploymentRepository.SaveChanges();

                    _logger.LogWarning("Upgrade failed for {StackName}. Deployment can be retried or rolled back.", request.StackName);
                }
                return new DeployStackResponse
                {
                    Success = false,
                    Message = "Deployment failed",
                    Errors = result.Errors
                };
            }

            // Report progress
            if (progressCallback != null)
            {
                await progressCallback("Persisting", "Saving deployment record...", 95, null, result.DeployedContexts.Count, result.DeployedContexts.Count, 0, 0);
            }

            DeploymentId deploymentId;
            Domain.Deployment.Deployments.Deployment deployment;

            if (isUpgrade)
            {
                // For upgrades: update the existing deployment in-place
                deployment = existingDeployment!;
                deploymentId = deployment.Id;

                // Update stack ID if it changed (e.g., different version in catalog)
                if (!string.IsNullOrEmpty(request.CatalogStackId))
                {
                    deployment.SetStackId(request.CatalogStackId);
                }

                deployment.SetStackVersion(plan.StackVersion);

                if (request.Variables != null && request.Variables.Count > 0)
                {
                    deployment.SetVariables(request.Variables);
                }
                if (request.MaintenanceObserver != null)
                {
                    deployment.SetMaintenanceObserverConfig(request.MaintenanceObserver);
                }
                if (request.HealthCheckConfigs != null && request.HealthCheckConfigs.Count > 0)
                {
                    deployment.SetHealthCheckConfigs(request.HealthCheckConfigs);
                }

                // Track upgrade history
                deployment.RecordUpgrade(previousVersion!, plan.StackVersion);

                // Remove old services and add new ones
                foreach (var service in deployment.Services.ToList())
                {
                    deployment.RemoveService(service.ServiceName);
                }
                foreach (var container in result.DeployedContainers)
                {
                    deployment.AddService(container.ServiceName, container.Image, "starting");
                    deployment.SetServiceContainerInfo(container.ServiceName, container.ContainerId, container.ContainerName, container.Status);
                }

                deployment.MarkAsRunning();
                _deploymentRepository.SaveChanges();
            }
            else
            {
                // For new installs: create new deployment
                deploymentId = _deploymentRepository.NextIdentity();
                deployment = Domain.Deployment.Deployments.Deployment.StartInstallation(
                    deploymentId,
                    new EnvironmentId(envGuid),
                    request.CatalogStackId ?? request.StackName,
                    request.StackName,
                    request.StackName,
                    deploymentUserId);

                deployment.SetStackVersion(plan.StackVersion);

                if (request.Variables != null && request.Variables.Count > 0)
                {
                    deployment.SetVariables(request.Variables);
                }
                if (request.MaintenanceObserver != null)
                {
                    deployment.SetMaintenanceObserverConfig(request.MaintenanceObserver);
                }
                if (request.HealthCheckConfigs != null && request.HealthCheckConfigs.Count > 0)
                {
                    deployment.SetHealthCheckConfigs(request.HealthCheckConfigs);
                }

                // Add services from deployment result
                foreach (var container in result.DeployedContainers)
                {
                    deployment.AddService(container.ServiceName, container.Image, "starting");
                    deployment.SetServiceContainerInfo(container.ServiceName, container.ContainerId, container.ContainerName, container.Status);
                }

                deployment.MarkAsRunning();
                _deploymentRepository.Add(deployment);
                _deploymentRepository.SaveChanges();
            }

            _logger.LogInformation(
                isUpgrade
                    ? "Successfully upgraded stack {StackName} from {OldVersion} to {NewVersion}"
                    : "Successfully deployed stack {StackName} with deployment ID {DeploymentId}",
                request.StackName, previousVersion ?? plan.StackVersion, isUpgrade ? plan.StackVersion : deploymentId.ToString());

            // Build success message with warning hint
            var message = $"Successfully deployed {request.StackName}";
            if (result.Warnings.Count > 0)
            {
                message += $" (with {result.Warnings.Count} warning{(result.Warnings.Count > 1 ? "s" : "")})";
            }

            // Report completion
            if (progressCallback != null)
            {
                await progressCallback("Complete", message, 100, null, result.DeployedContexts.Count, result.DeployedContexts.Count, 0, 0);
            }

            return new DeployStackResponse
            {
                Success = true,
                Message = message,
                DeploymentId = deploymentId.ToString(),
                StackName = request.StackName,
                StackVersion = plan.StackVersion,
                Services = result.DeployedContexts.Select(c => new DeployedServiceInfo
                {
                    ServiceName = c,
                    Status = "running"
                }).ToList(),
                Warnings = result.Warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy stack {StackName}", request.StackName);

            // If this was an upgrade and we have an existing deployment, mark it as failed
            // This prevents the deployment from being stuck in "Upgrading" status
            if (isUpgrade && existingDeployment != null)
            {
                try
                {
                    existingDeployment.MarkAsFailed($"Exception during upgrade: {ex.Message}");
                    _deploymentRepository.SaveChanges();
                    _logger.LogWarning("Marked deployment {DeploymentId} as failed due to exception", existingDeployment.Id);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to mark deployment as failed after exception");
                }
            }

            return DeployStackResponse.Failed($"Deployment failed: {ex.Message}", ex.Message);
        }
    }

    /// <summary>
    /// Creates a deployment plan directly from structured ServiceTemplate data.
    /// No YAML parsing needed - data is already in structured format from catalog.
    /// </summary>
    private DeploymentPlan CreateDeploymentPlanFromStructuredData(DeployStackRequest request)
    {
        var variables = request.Variables ?? new Dictionary<string, string>();
        var plan = new DeploymentPlan
        {
            StackVersion = request.StackVersion ?? "unspecified",
            StackName = request.StackName,
            GlobalEnvVars = variables
        };

        // Sanitize stack name for Docker naming (replace spaces with underscores)
        var sanitizedStackName = request.StackName.Replace(" ", "_");

        // Process networks
        foreach (var network in request.Networks)
        {
            var resolvedName = network.External
                ? network.Name
                : $"{sanitizedStackName}_{network.Name}";

            plan.Networks[network.Name] = new NetworkDefinition
            {
                External = network.External,
                ResolvedName = resolvedName
            };
        }

        // Process services into deployment steps
        var order = 0;
        foreach (var service in request.Services)
        {
            // Resolve variables in all service configuration fields
            var resolvedImage = ResolveEnvironmentValue(service.Image, variables);
            var resolvedContainerName = service.ContainerName != null
                ? ResolveEnvironmentValue(service.ContainerName, variables)
                : $"{sanitizedStackName}_{service.Name}";

            var step = new DeploymentStep
            {
                ContextName = service.Name,
                Image = resolvedImage,
                Version = ExtractImageVersion(resolvedImage),
                ContainerName = resolvedContainerName,
                Order = order++,
                DependsOn = service.DependsOn.ToList(),
                Lifecycle = service.Lifecycle
            };

            // Map ports - resolve ${VAR} placeholders in port mappings
            step.Ports = service.Ports
                .Select(p =>
                {
                    var hostPort = ResolveEnvironmentValue(p.HostPort, variables);
                    var containerPort = ResolveEnvironmentValue(p.ContainerPort, variables);
                    return $"{hostPort}:{containerPort}{(string.IsNullOrEmpty(p.Protocol) ? "" : "/" + p.Protocol)}";
                })
                .ToList();

            // Determine if service is internal (no exposed ports)
            step.Internal = !service.Ports.Any();

            // Map volumes - resolve ${VAR} placeholders in volume mappings
            foreach (var vol in service.Volumes)
            {
                var source = ResolveEnvironmentValue(vol.Source, variables);
                var target = ResolveEnvironmentValue(vol.Target, variables);

                // Prefix named volumes with stack name (unless external)
                if (!source.StartsWith("/") && !source.StartsWith("./") && !source.Contains(":"))
                {
                    var volumeDef = request.Volumes.FirstOrDefault(v => v.Name == vol.Source);
                    if (volumeDef == null || !volumeDef.External)
                    {
                        source = $"{sanitizedStackName}_{source}";
                    }
                }
                step.Volumes[source] = target;
            }

            // Map environment variables - resolve ${VAR} placeholders
            foreach (var env in service.Environment)
            {
                step.EnvVars[env.Key] = ResolveEnvironmentValue(env.Value, variables);
            }

            // Map networks
            step.Networks = service.Networks
                .Select(n =>
                {
                    // Check if network is external
                    var networkDef = request.Networks.FirstOrDefault(nd => nd.Name == n);
                    return networkDef?.External == true ? n : $"{sanitizedStackName}_{n}";
                })
                .ToList();

            plan.Steps.Add(step);
        }

        // Re-order steps based on dependencies (topological sort)
        ReorderStepsByDependencies(plan.Steps);

        return plan;
    }

    /// <summary>
    /// Extracts version tag from Docker image string.
    /// </summary>
    private static string ExtractImageVersion(string image)
    {
        var colonIndex = image.LastIndexOf(':');
        if (colonIndex > 0 && !image.Substring(colonIndex).Contains('/'))
        {
            return image.Substring(colonIndex + 1);
        }
        return "latest";
    }

    /// <summary>
    /// Resolves environment variable placeholders like ${VAR} or ${VAR:-default}.
    /// </summary>
    private static string ResolveEnvironmentValue(string value, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Match ${VAR} or ${VAR:-default} patterns
        var result = System.Text.RegularExpressions.Regex.Replace(value, @"\$\{([^}:]+)(?::-([^}]*))?\}", match =>
        {
            var varName = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : "";

            if (variables.TryGetValue(varName, out var resolvedValue) && !string.IsNullOrEmpty(resolvedValue))
            {
                return resolvedValue;
            }
            return defaultValue;
        });

        return result;
    }

    /// <summary>
    /// Reorders deployment steps based on dependencies (topological sort).
    /// </summary>
    private static void ReorderStepsByDependencies(List<DeploymentStep> steps)
    {
        var sorted = new List<DeploymentStep>();
        var visited = new HashSet<string>();
        var stepMap = steps.ToDictionary(s => s.ContextName);

        void Visit(DeploymentStep step)
        {
            if (visited.Contains(step.ContextName))
                return;

            visited.Add(step.ContextName);

            foreach (var dep in step.DependsOn)
            {
                if (stepMap.TryGetValue(dep, out var depStep))
                {
                    Visit(depStep);
                }
            }

            sorted.Add(step);
        }

        foreach (var step in steps)
        {
            Visit(step);
        }

        // Update order based on sorted position
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].Order = i;
        }

        // Replace original list contents
        steps.Clear();
        steps.AddRange(sorted);
    }
}
