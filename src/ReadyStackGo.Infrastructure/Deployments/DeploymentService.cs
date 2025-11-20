using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Deployments;
using ReadyStackGo.Application.Manifests;
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

            _logger.LogInformation("Successfully deployed stack {StackName} with deployment ID {DeploymentId}",
                request.StackName, deploymentId);

            return new DeployComposeResponse
            {
                Success = true,
                Message = $"Successfully deployed {request.StackName}",
                DeploymentId = deploymentId,
                StackName = request.StackName,
                Services = result.DeployedContexts.Select(c => new DeployedServiceInfo
                {
                    ServiceName = c,
                    Status = "running"
                }).ToList()
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

            // For now, return a placeholder - full implementation would read from config store
            return await Task.FromResult(new GetDeploymentResponse
            {
                Success = false,
                Message = $"Deployment '{stackName}' not found in environment '{environmentId}'"
            });
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

            // For now, return empty list - full implementation would read from config store
            return await Task.FromResult(new ListDeploymentsResponse
            {
                Success = true,
                Deployments = new List<DeploymentSummary>()
            });
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
            var result = await _deploymentEngine.RemoveStackAsync(stackName);

            if (!result.Success)
            {
                return new DeployComposeResponse
                {
                    Success = false,
                    Message = "Failed to remove deployment",
                    Errors = result.Errors
                };
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
