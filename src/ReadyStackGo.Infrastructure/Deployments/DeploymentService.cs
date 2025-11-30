using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Domain.Identity.Repositories;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.StackManagement.Aggregates;
using ReadyStackGo.Domain.StackManagement.Repositories;
using ReadyStackGo.Domain.StackManagement.ValueObjects;
using ReadyStackGo.Infrastructure.Deployment;

namespace ReadyStackGo.Infrastructure.Deployments;

/// <summary>
/// Service for managing Docker Compose stack deployments.
/// v0.6: Fully migrated to SQLite persistence.
/// </summary>
public class DeploymentService : IDeploymentService
{
    private readonly IDockerComposeParser _composeParser;
    private readonly IDeploymentEngine _deploymentEngine;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DeploymentService> _logger;

    public DeploymentService(
        IDockerComposeParser composeParser,
        IDeploymentEngine deploymentEngine,
        IDeploymentRepository deploymentRepository,
        IEnvironmentRepository environmentRepository,
        IUserRepository userRepository,
        ILogger<DeploymentService> logger)
    {
        _composeParser = composeParser;
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
            _logger.LogInformation("Parsing Docker Compose file");

            // Validate the compose file
            var validation = await _composeParser.ValidateAsync(request.YamlContent);

            if (!validation.IsValid)
            {
                return new ParseComposeResponse
                {
                    Success = false,
                    Message = "Compose file validation failed",
                    Errors = validation.Errors,
                    Warnings = validation.Warnings
                };
            }

            // Parse the compose file
            var definition = await _composeParser.ParseAsync(request.YamlContent);

            // Detect environment variables
            var variables = await _composeParser.DetectVariablesAsync(request.YamlContent);

            return new ParseComposeResponse
            {
                Success = true,
                Message = $"Successfully parsed compose file with {definition.Services.Count} services",
                Services = definition.Services.Keys.ToList(),
                Variables = variables.Select(v => new EnvironmentVariableInfo
                {
                    Name = v.Name,
                    DefaultValue = v.DefaultValue,
                    IsRequired = v.IsRequired,
                    UsedInServices = v.UsedInServices
                }).ToList(),
                Warnings = validation.Warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Docker Compose file");
            return new ParseComposeResponse
            {
                Success = false,
                Message = $"Failed to parse compose file: {ex.Message}",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<DeployComposeResponse> DeployComposeAsync(string environmentId, DeployComposeRequest request)
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

            // Parse and validate the compose file
            var validation = await _composeParser.ValidateAsync(request.YamlContent);
            if (!validation.IsValid)
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Compose file validation failed",
                    Errors = validation.Errors
                };
            }

            // Parse the compose file
            var definition = await _composeParser.ParseAsync(request.YamlContent);

            // Convert to deployment plan
            var plan = await _composeParser.ConvertToDeploymentPlanAsync(
                definition,
                request.Variables,
                request.StackName);

            // Set environment ID for the deployment
            plan.EnvironmentId = environmentId;
            plan.StackName = request.StackName;

            // Execute deployment
            var result = await _deploymentEngine.ExecuteDeploymentAsync(plan);

            if (!result.Success)
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Deployment failed",
                    Errors = result.Errors
                };
            }

            // Get current user (use first admin for now - TODO: get from authentication context)
            var currentUser = _userRepository.GetAll().FirstOrDefault();
            var userId = currentUser?.Id ?? new UserId();

            // Create and persist deployment record
            var deploymentId = _deploymentRepository.NextIdentity();
            var deployment = Domain.StackManagement.Aggregates.Deployment.Start(
                deploymentId,
                new EnvironmentId(envGuid),
                request.StackName,
                request.StackName, // Use stack name as project name
                userId);

            deployment.SetStackVersion(plan.StackVersion ?? "1.0.0");

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

            var deployments = _deploymentRepository.GetByEnvironment(new EnvironmentId(envGuid));

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

            // Remove deployment record from database
            var deployment = _deploymentRepository.GetByStackName(new EnvironmentId(envGuid), stackName);
            if (deployment != null)
            {
                deployment.MarkAsRemoved();
                _deploymentRepository.Update(deployment);
                _deploymentRepository.SaveChanges();
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
}
