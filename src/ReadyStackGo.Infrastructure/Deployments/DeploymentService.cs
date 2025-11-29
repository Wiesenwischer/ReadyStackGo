using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Deployments;
using ReadyStackGo.Application.Manifests;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Deployment;

namespace ReadyStackGo.Infrastructure.Deployments;

/// <summary>
/// Service for managing Docker Compose stack deployments.
/// v0.4: Supports deploying stacks to specific environments.
/// </summary>
public class DeploymentService : IDeploymentService
{
    private readonly IDockerComposeParser _composeParser;
    private readonly IDeploymentEngine _deploymentEngine;
    private readonly IConfigStore _configStore;
    private readonly ILogger<DeploymentService> _logger;

    public DeploymentService(
        IDockerComposeParser composeParser,
        IDeploymentEngine deploymentEngine,
        IConfigStore configStore,
        ILogger<DeploymentService> logger)
    {
        _composeParser = composeParser;
        _deploymentEngine = deploymentEngine;
        _configStore = configStore;
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
            var systemConfig = await _configStore.GetSystemConfigAsync();
            var environment = systemConfig.Organization?.GetEnvironment(environmentId);

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

            // Generate deployment ID
            var deploymentId = $"{environmentId}-{request.StackName}-{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Persist deployment record
            var deploymentsConfig = await _configStore.GetDeploymentsConfigAsync();
            var deploymentRecord = new DeploymentRecord
            {
                StackName = request.StackName,
                StackVersion = plan.StackVersion,
                DeploymentId = deploymentId,
                DeployedAt = DateTime.UtcNow,
                Status = "running",
                Services = result.DeployedContexts.Select(c => new DeployedService
                {
                    ServiceName = c,
                    ContainerName = c, // Will be refined when we have container name info
                    Image = "unknown", // Will be refined when we have image info
                    Status = "running"
                }).ToList()
            };
            deploymentsConfig.SetDeployment(environmentId, deploymentRecord);
            await _configStore.SaveDeploymentsConfigAsync(deploymentsConfig);

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
                DeploymentId = deploymentId,
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

    public async Task<GetDeploymentResponse> GetDeploymentAsync(string environmentId, string stackName)
    {
        try
        {
            _logger.LogInformation("Getting deployment {StackName} in environment {EnvironmentId}",
                stackName, environmentId);

            var deploymentsConfig = await _configStore.GetDeploymentsConfigAsync();
            var deployment = deploymentsConfig.GetDeployment(environmentId, stackName);

            if (deployment == null)
            {
                return new GetDeploymentResponse
                {
                    Success = false,
                    Message = $"Deployment '{stackName}' not found in environment '{environmentId}'"
                };
            }

            return new GetDeploymentResponse
            {
                Success = true,
                StackName = deployment.StackName,
                StackVersion = deployment.StackVersion,
                DeploymentId = deployment.DeploymentId,
                DeployedAt = deployment.DeployedAt,
                Status = deployment.Status,
                Services = deployment.Services.Select(s => new DeployedServiceInfo
                {
                    ServiceName = s.ServiceName,
                    Status = s.Status
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get deployment {StackName}", stackName);
            return new GetDeploymentResponse
            {
                Success = false,
                Message = $"Failed to get deployment: {ex.Message}"
            };
        }
    }

    public async Task<ListDeploymentsResponse> ListDeploymentsAsync(string environmentId)
    {
        try
        {
            _logger.LogInformation("Listing deployments in environment {EnvironmentId}", environmentId);

            var deploymentsConfig = await _configStore.GetDeploymentsConfigAsync();
            var deployments = deploymentsConfig.GetDeploymentsForEnvironment(environmentId);

            return new ListDeploymentsResponse
            {
                Success = true,
                Deployments = deployments.Select(d => new DeploymentSummary
                {
                    StackName = d.StackName,
                    StackVersion = d.StackVersion,
                    DeploymentId = d.DeploymentId,
                    DeployedAt = d.DeployedAt,
                    Status = d.Status,
                    ServiceCount = d.Services.Count
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list deployments");
            return new ListDeploymentsResponse
            {
                Success = false,
                Deployments = new List<DeploymentSummary>()
            };
        }
    }

    public async Task<DeployComposeResponse> RemoveDeploymentAsync(string environmentId, string stackName)
    {
        try
        {
            _logger.LogInformation("Removing deployment {StackName} from environment {EnvironmentId}",
                stackName, environmentId);

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

            // Remove deployment record from persistence
            var deploymentsConfig = await _configStore.GetDeploymentsConfigAsync();
            deploymentsConfig.RemoveDeployment(environmentId, stackName);
            await _configStore.SaveDeploymentsConfigAsync(deploymentsConfig);

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
