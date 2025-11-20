using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Environments;
using ReadyStackGo.Domain.Organizations;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Environments;

/// <summary>
/// Service for managing environments within an organization.
/// v0.4: Only DockerSocketEnvironment is supported.
/// </summary>
public class EnvironmentService : IEnvironmentService
{
    private readonly IConfigStore _configStore;
    private readonly ILogger<EnvironmentService> _logger;

    public EnvironmentService(IConfigStore configStore, ILogger<EnvironmentService> logger)
    {
        _configStore = configStore;
        _logger = logger;
    }

    public async Task<ListEnvironmentsResponse> GetEnvironmentsAsync()
    {
        var systemConfig = await _configStore.GetSystemConfigAsync();

        if (systemConfig.Organization == null)
        {
            return new ListEnvironmentsResponse { Environments = new List<EnvironmentResponse>() };
        }

        var environments = systemConfig.Organization.Environments
            .Select(MapToResponse)
            .ToList();

        return new ListEnvironmentsResponse { Environments = environments };
    }

    public async Task<EnvironmentResponse?> GetEnvironmentAsync(string environmentId)
    {
        var systemConfig = await _configStore.GetSystemConfigAsync();

        var environment = systemConfig.Organization?.GetEnvironment(environmentId);

        return environment != null ? MapToResponse(environment) : null;
    }

    public async Task<CreateEnvironmentResponse> CreateEnvironmentAsync(CreateEnvironmentRequest request)
    {
        try
        {
            _logger.LogInformation("Creating environment: {Id} - {Name}", request.Id, request.Name);

            var systemConfig = await _configStore.GetSystemConfigAsync();

            if (systemConfig.Organization == null)
            {
                return new CreateEnvironmentResponse
                {
                    Success = false,
                    Message = "Organization not set. Complete the setup wizard first."
                };
            }

            // Create DockerSocketEnvironment (only type supported in v0.4)
            var environment = new DockerSocketEnvironment
            {
                Id = request.Id,
                Name = request.Name,
                SocketPath = request.SocketPath,
                CreatedAt = DateTime.UtcNow
            };

            // Add to organization (validates uniqueness)
            systemConfig.Organization.AddEnvironment(environment);

            await _configStore.SaveSystemConfigAsync(systemConfig);

            _logger.LogInformation("Environment created successfully: {Id}", request.Id);

            return new CreateEnvironmentResponse
            {
                Success = true,
                Message = "Environment created successfully",
                Environment = MapToResponse(environment)
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create environment: {Id}", request.Id);
            return new CreateEnvironmentResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating environment: {Id}", request.Id);
            return new CreateEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to create environment: {ex.Message}"
            };
        }
    }

    public async Task<UpdateEnvironmentResponse> UpdateEnvironmentAsync(string environmentId, UpdateEnvironmentRequest request)
    {
        try
        {
            _logger.LogInformation("Updating environment: {Id}", environmentId);

            var systemConfig = await _configStore.GetSystemConfigAsync();

            if (systemConfig.Organization == null)
            {
                return new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = "Organization not set."
                };
            }

            var environment = systemConfig.Organization.GetEnvironment(environmentId);

            if (environment == null)
            {
                return new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment '{environmentId}' not found."
                };
            }

            // Check if new socket path conflicts with another environment
            var newConnectionString = request.SocketPath;
            var conflicting = systemConfig.Organization.Environments
                .FirstOrDefault(e => e.Id != environmentId && e.GetConnectionString() == newConnectionString);

            if (conflicting != null)
            {
                return new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = $"Socket path '{request.SocketPath}' is already used by environment '{conflicting.Id}'."
                };
            }

            // Update properties
            environment.Name = request.Name;

            if (environment is DockerSocketEnvironment dockerEnv)
            {
                dockerEnv.SocketPath = request.SocketPath;
            }

            systemConfig.Organization.UpdatedAt = DateTime.UtcNow;

            await _configStore.SaveSystemConfigAsync(systemConfig);

            _logger.LogInformation("Environment updated successfully: {Id}", environmentId);

            return new UpdateEnvironmentResponse
            {
                Success = true,
                Message = "Environment updated successfully",
                Environment = MapToResponse(environment)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update environment: {Id}", environmentId);
            return new UpdateEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to update environment: {ex.Message}"
            };
        }
    }

    public async Task<DeleteEnvironmentResponse> DeleteEnvironmentAsync(string environmentId)
    {
        try
        {
            _logger.LogInformation("Deleting environment: {Id}", environmentId);

            var systemConfig = await _configStore.GetSystemConfigAsync();

            if (systemConfig.Organization == null)
            {
                return new DeleteEnvironmentResponse
                {
                    Success = false,
                    Message = "Organization not set."
                };
            }

            systemConfig.Organization.RemoveEnvironment(environmentId);

            await _configStore.SaveSystemConfigAsync(systemConfig);

            _logger.LogInformation("Environment deleted successfully: {Id}", environmentId);

            return new DeleteEnvironmentResponse
            {
                Success = true,
                Message = "Environment deleted successfully"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete environment: {Id}", environmentId);
            return new DeleteEnvironmentResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting environment: {Id}", environmentId);
            return new DeleteEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to delete environment: {ex.Message}"
            };
        }
    }

    public async Task<SetDefaultEnvironmentResponse> SetDefaultEnvironmentAsync(string environmentId)
    {
        try
        {
            _logger.LogInformation("Setting default environment: {Id}", environmentId);

            var systemConfig = await _configStore.GetSystemConfigAsync();

            if (systemConfig.Organization == null)
            {
                return new SetDefaultEnvironmentResponse
                {
                    Success = false,
                    Message = "Organization not set."
                };
            }

            systemConfig.Organization.SetDefaultEnvironment(environmentId);

            await _configStore.SaveSystemConfigAsync(systemConfig);

            _logger.LogInformation("Default environment set successfully: {Id}", environmentId);

            return new SetDefaultEnvironmentResponse
            {
                Success = true,
                Message = "Default environment set successfully"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to set default environment: {Id}", environmentId);
            return new SetDefaultEnvironmentResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error setting default environment: {Id}", environmentId);
            return new SetDefaultEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to set default environment: {ex.Message}"
            };
        }
    }

    private static EnvironmentResponse MapToResponse(Domain.Organizations.Environment environment)
    {
        return new EnvironmentResponse
        {
            Id = environment.Id,
            Name = environment.Name,
            Type = environment.GetEnvironmentType(),
            ConnectionString = environment.GetConnectionString(),
            IsDefault = environment.IsDefault,
            CreatedAt = environment.CreatedAt
        };
    }
}
