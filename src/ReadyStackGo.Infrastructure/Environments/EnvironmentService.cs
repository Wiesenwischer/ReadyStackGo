using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Domain.IdentityAccess.Repositories;
using ReadyStackGo.Domain.Deployment.Aggregates;
using ReadyStackGo.Domain.Deployment.Repositories;
using ReadyStackGo.Domain.Deployment.ValueObjects;
using DomainEnvironment = ReadyStackGo.Domain.Deployment.Aggregates.Environment;

namespace ReadyStackGo.Infrastructure.Environments;

/// <summary>
/// Service for managing environments within an organization.
/// v0.6: Fully migrated to SQLite persistence.
/// </summary>
public class EnvironmentService : IEnvironmentService
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<EnvironmentService> _logger;

    public EnvironmentService(
        IEnvironmentRepository environmentRepository,
        IOrganizationRepository organizationRepository,
        ILogger<EnvironmentService> logger)
    {
        _environmentRepository = environmentRepository;
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public Task<ListEnvironmentsResponse> GetEnvironmentsAsync()
    {
        var organization = _organizationRepository.GetAll().FirstOrDefault();

        if (organization == null)
        {
            return Task.FromResult(new ListEnvironmentsResponse
            {
                Success = true,
                Environments = new List<EnvironmentResponse>()
            });
        }

        var environments = _environmentRepository.GetByOrganization(organization.Id)
            .Select(MapToResponse)
            .ToList();

        return Task.FromResult(new ListEnvironmentsResponse
        {
            Success = true,
            Environments = environments
        });
    }

    public Task<EnvironmentResponse?> GetEnvironmentAsync(string environmentId)
    {
        if (!Guid.TryParse(environmentId, out var guid))
        {
            return Task.FromResult<EnvironmentResponse?>(null);
        }

        var environment = _environmentRepository.Get(new EnvironmentId(guid));

        return Task.FromResult(environment != null ? MapToResponse(environment) : null);
    }

    public Task<CreateEnvironmentResponse> CreateEnvironmentAsync(CreateEnvironmentRequest request)
    {
        try
        {
            _logger.LogInformation("Creating environment: {Name}", request.Name);

            var organization = _organizationRepository.GetAll().FirstOrDefault();

            if (organization == null)
            {
                return Task.FromResult(new CreateEnvironmentResponse
                {
                    Success = false,
                    Message = "Organization not set. Complete the setup wizard first."
                });
            }

            // Check for duplicate name
            var existingByName = _environmentRepository.GetByName(organization.Id, request.Name);
            if (existingByName != null)
            {
                return Task.FromResult(new CreateEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment with name '{request.Name}' already exists."
                });
            }

            // Create environment
            var environmentId = _environmentRepository.NextIdentity();
            var environment = DomainEnvironment.CreateDockerSocket(
                environmentId,
                organization.Id,
                request.Name,
                null,
                request.SocketPath);

            // Set as default if this is the first environment
            var existingEnvironments = _environmentRepository.GetByOrganization(organization.Id);
            if (!existingEnvironments.Any())
            {
                environment.SetAsDefault();
            }

            _environmentRepository.Add(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Environment created successfully: {Id}", environmentId);

            return Task.FromResult(new CreateEnvironmentResponse
            {
                Success = true,
                Message = "Environment created successfully",
                Environment = MapToResponse(environment)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create environment: {Name}", request.Name);
            return Task.FromResult(new CreateEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to create environment: {ex.Message}"
            });
        }
    }

    public Task<UpdateEnvironmentResponse> UpdateEnvironmentAsync(string environmentId, UpdateEnvironmentRequest request)
    {
        try
        {
            _logger.LogInformation("Updating environment: {Id}", environmentId);

            if (!Guid.TryParse(environmentId, out var guid))
            {
                return Task.FromResult(new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = "Invalid environment ID."
                });
            }

            var environment = _environmentRepository.Get(new EnvironmentId(guid));

            if (environment == null)
            {
                return Task.FromResult(new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment '{environmentId}' not found."
                });
            }

            // Check for duplicate name (exclude current environment)
            var existingByName = _environmentRepository.GetByName(environment.OrganizationId, request.Name);
            if (existingByName != null && existingByName.Id != environment.Id)
            {
                return Task.FromResult(new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment with name '{request.Name}' already exists."
                });
            }

            // Update properties
            environment.UpdateName(request.Name);
            environment.UpdateConnectionConfig(ConnectionConfig.DockerSocket(request.SocketPath));

            _environmentRepository.Update(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Environment updated successfully: {Id}", environmentId);

            return Task.FromResult(new UpdateEnvironmentResponse
            {
                Success = true,
                Message = "Environment updated successfully",
                Environment = MapToResponse(environment)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update environment: {Id}", environmentId);
            return Task.FromResult(new UpdateEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to update environment: {ex.Message}"
            });
        }
    }

    public Task<DeleteEnvironmentResponse> DeleteEnvironmentAsync(string environmentId)
    {
        try
        {
            _logger.LogInformation("Deleting environment: {Id}", environmentId);

            if (!Guid.TryParse(environmentId, out var guid))
            {
                return Task.FromResult(new DeleteEnvironmentResponse
                {
                    Success = false,
                    Message = "Invalid environment ID."
                });
            }

            var environment = _environmentRepository.Get(new EnvironmentId(guid));

            if (environment == null)
            {
                return Task.FromResult(new DeleteEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment '{environmentId}' not found."
                });
            }

            if (environment.IsDefault)
            {
                return Task.FromResult(new DeleteEnvironmentResponse
                {
                    Success = false,
                    Message = "Cannot delete the default environment. Set another environment as default first."
                });
            }

            _environmentRepository.Remove(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Environment deleted successfully: {Id}", environmentId);

            return Task.FromResult(new DeleteEnvironmentResponse
            {
                Success = true,
                Message = "Environment deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete environment: {Id}", environmentId);
            return Task.FromResult(new DeleteEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to delete environment: {ex.Message}"
            });
        }
    }

    public Task<SetDefaultEnvironmentResponse> SetDefaultEnvironmentAsync(string environmentId)
    {
        try
        {
            _logger.LogInformation("Setting default environment: {Id}", environmentId);

            if (!Guid.TryParse(environmentId, out var guid))
            {
                return Task.FromResult(new SetDefaultEnvironmentResponse
                {
                    Success = false,
                    Message = "Invalid environment ID."
                });
            }

            var environment = _environmentRepository.Get(new EnvironmentId(guid));

            if (environment == null)
            {
                return Task.FromResult(new SetDefaultEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment '{environmentId}' not found."
                });
            }

            // Unset current default
            var currentDefault = _environmentRepository.GetDefault(environment.OrganizationId);
            if (currentDefault != null && currentDefault.Id != environment.Id)
            {
                currentDefault.UnsetAsDefault();
                _environmentRepository.Update(currentDefault);
            }

            // Set new default
            environment.SetAsDefault();
            _environmentRepository.Update(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Default environment set successfully: {Id}", environmentId);

            return Task.FromResult(new SetDefaultEnvironmentResponse
            {
                Success = true,
                Message = "Default environment set successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set default environment: {Id}", environmentId);
            return Task.FromResult(new SetDefaultEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to set default environment: {ex.Message}"
            });
        }
    }

    private static EnvironmentResponse MapToResponse(DomainEnvironment environment)
    {
        return new EnvironmentResponse
        {
            Id = environment.Id.ToString(),
            Name = environment.Name,
            Type = environment.Type.ToString(),
            ConnectionString = environment.ConnectionConfig.SocketPath,
            IsDefault = environment.IsDefault,
            CreatedAt = environment.CreatedAt
        };
    }
}
