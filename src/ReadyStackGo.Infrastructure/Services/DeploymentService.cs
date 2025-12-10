using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Infrastructure.Services;

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
            _logger.LogInformation("Deploying stack {StackName} to environment {EnvironmentId}",
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
                await progressCallback("Validating", "Validating manifest...", 5, null, 0, 0);
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
                await progressCallback("Parsing", "Parsing RSGo manifest...", 10, null, 0, 0);
            }

            // Parse manifest once
            var manifest = await _manifestParser.ParseAsync(request.YamlContent);

            if (progressCallback != null)
            {
                await progressCallback("Planning", "Creating deployment plan...", 15, null, 0, 0);
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
                engineCallback = async (phase, message, percent, currentService, totalServices, completedServices) =>
                {
                    // Scale the engine progress (0-100) to our range (20-90)
                    var scaledPercent = 20 + (percent * 70 / 100);
                    await progressCallback(phase, message, scaledPercent, currentService, totalServices, completedServices);
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
                await progressCallback("Persisting", "Saving deployment record...", 95, null, result.DeployedContexts.Count, result.DeployedContexts.Count);
            }

            // Get current user (use first admin for now - TODO: get from authentication context)
            var currentUser = _userRepository.GetAll().FirstOrDefault();
            var userId = currentUser?.Id ?? new UserId();

            // Create and persist deployment record
            var deploymentId = _deploymentRepository.NextIdentity();
            var deployment = Domain.Deployment.Deployments.Deployment.Start(
                deploymentId,
                new EnvironmentId(envGuid),
                request.StackName,
                request.StackName, // Use stack name as project name
                userId);

            deployment.SetStackVersion(plan.StackVersion);

            // Store deployment variables for later use (e.g., maintenance observer)
            if (request.Variables != null && request.Variables.Count > 0)
            {
                deployment.SetVariables(request.Variables);
            }

            // Mark as running with services
            var deployedServices = result.DeployedContexts.Select(c => new DeployedService(
                c,
                null, // Container ID will be populated by container inspection
                c,    // Container name
                null, // Image
                "running"
            )).ToList();

            deployment.MarkAsRunning(deployedServices);

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
                await progressCallback("Complete", message, 100, null, result.DeployedContexts.Count, result.DeployedContexts.Count);
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
                    _deploymentRepository.Update(deployment);
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
                _deploymentRepository.Update(deployment);
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

    public async Task<DeployStackResponse> DeployStackAsync(
        string environmentId,
        DeployStackRequest request,
        DeploymentServiceProgressCallback? progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deploying stack {StackName} to environment {EnvironmentId} (catalog: {CatalogStackId})",
                request.StackName, environmentId, request.CatalogStackId);

            // Validate environment exists
            if (!Guid.TryParse(environmentId, out var envGuid))
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
                await progressCallback("Validating", "Validating manifest...", 5, null, 0, 0);
            }

            // Validate manifest
            var validation = await _manifestParser.ValidateAsync(request.YamlContent);
            if (!validation.IsValid)
            {
                return new DeployStackResponse
                {
                    Success = false,
                    Message = "Manifest validation failed",
                    Errors = validation.Errors
                };
            }

            // Report progress
            if (progressCallback != null)
            {
                await progressCallback("Parsing", "Parsing RSGo manifest...", 10, null, 0, 0);
            }

            // Parse manifest once
            var manifest = await _manifestParser.ParseAsync(request.YamlContent);

            if (progressCallback != null)
            {
                await progressCallback("Planning", "Creating deployment plan...", 15, null, 0, 0);
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
                engineCallback = async (phase, message, percent, currentService, totalServices, completedServices) =>
                {
                    // Scale the engine progress (0-100) to our range (20-90)
                    var scaledPercent = 20 + (percent * 70 / 100);
                    await progressCallback(phase, message, scaledPercent, currentService, totalServices, completedServices);
                };
            }

            // Execute deployment with progress callback
            var result = await _deploymentEngine.ExecuteDeploymentAsync(plan, engineCallback, cancellationToken);

            if (!result.Success)
            {
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
                await progressCallback("Persisting", "Saving deployment record...", 95, null, result.DeployedContexts.Count, result.DeployedContexts.Count);
            }

            // Get current user (use first admin for now - TODO: get from authentication context)
            var currentUser = _userRepository.GetAll().FirstOrDefault();
            var userId = currentUser?.Id ?? new UserId();

            // Create and persist deployment record
            var deploymentId = _deploymentRepository.NextIdentity();
            var deployment = Domain.Deployment.Deployments.Deployment.Start(
                deploymentId,
                new EnvironmentId(envGuid),
                request.StackName,
                request.StackName, // Use stack name as project name
                userId);

            deployment.SetStackVersion(plan.StackVersion);

            // Store deployment variables for later use (e.g., maintenance observer)
            if (request.Variables != null && request.Variables.Count > 0)
            {
                deployment.SetVariables(request.Variables);
            }

            // Mark as running with services
            var deployedServices = result.DeployedContexts.Select(c => new DeployedService(
                c,
                null, // Container ID will be populated by container inspection
                c,    // Container name
                null, // Image
                "running"
            )).ToList();

            deployment.MarkAsRunning(deployedServices);

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
                await progressCallback("Complete", message, 100, null, result.DeployedContexts.Count, result.DeployedContexts.Count);
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
            return DeployStackResponse.Failed($"Deployment failed: {ex.Message}", ex.Message);
        }
    }
}
