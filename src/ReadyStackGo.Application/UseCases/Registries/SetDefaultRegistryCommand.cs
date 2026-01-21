namespace ReadyStackGo.Application.UseCases.Registries;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

/// <summary>
/// Command to set a registry as the default for the organization.
/// </summary>
public record SetDefaultRegistryCommand(string RegistryId) : IRequest<RegistryResponse>;

public class SetDefaultRegistryHandler : IRequestHandler<SetDefaultRegistryCommand, RegistryResponse>
{
    private readonly IRegistryRepository _registryRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<SetDefaultRegistryHandler> _logger;

    public SetDefaultRegistryHandler(
        IRegistryRepository registryRepository,
        IOrganizationRepository organizationRepository,
        ILogger<SetDefaultRegistryHandler> logger)
    {
        _registryRepository = registryRepository;
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public Task<RegistryResponse> Handle(SetDefaultRegistryCommand command, CancellationToken cancellationToken)
    {
        var organization = _organizationRepository.GetAll().FirstOrDefault();
        if (organization == null)
        {
            return Task.FromResult(new RegistryResponse(false, "Organization not set. Complete the setup wizard first."));
        }

        if (!Guid.TryParse(command.RegistryId, out var registryGuid))
        {
            return Task.FromResult(new RegistryResponse(false, "Invalid registry ID format"));
        }

        var organizationId = DeploymentOrganizationId.FromIdentityAccess(organization.Id);
        var registryId = new RegistryId(registryGuid);

        var registry = _registryRepository.GetById(registryId);
        if (registry == null)
        {
            return Task.FromResult(new RegistryResponse(false, "Registry not found"));
        }

        if (registry.OrganizationId != organizationId)
        {
            return Task.FromResult(new RegistryResponse(false, "Registry does not belong to this organization"));
        }

        try
        {
            // Unset current default if exists
            var currentDefault = _registryRepository.GetDefault(organizationId);
            if (currentDefault != null && currentDefault.Id != registryId)
            {
                currentDefault.UnsetAsDefault();
                _registryRepository.Update(currentDefault);
            }

            // Set new default
            registry.SetAsDefault();
            _registryRepository.Update(registry);
            _registryRepository.SaveChanges();

            _logger.LogInformation("Set registry {RegistryId} '{Name}' as default for organization {OrganizationId}",
                registryId, registry.Name, organizationId);

            var dto = new RegistryDto(
                Id: registry.Id.Value.ToString(),
                Name: registry.Name,
                Url: registry.Url,
                Username: registry.Username,
                HasCredentials: registry.HasCredentials,
                IsDefault: registry.IsDefault,
                ImagePatterns: registry.ImagePatterns,
                CreatedAt: registry.CreatedAt,
                UpdatedAt: registry.UpdatedAt
            );

            return Task.FromResult(new RegistryResponse(true, $"Registry '{registry.Name}' set as default", dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set default registry {RegistryId}", command.RegistryId);
            return Task.FromResult(new RegistryResponse(false, $"Failed to set default registry: {ex.Message}"));
        }
    }
}
