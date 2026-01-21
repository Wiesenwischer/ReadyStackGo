using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// Provides registry credentials from the database for Docker image pulls.
/// Matches images to registries using configured ImagePatterns.
/// </summary>
public class RegistryCredentialProvider : IRegistryCredentialProvider
{
    private readonly IRegistryRepository _registryRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<RegistryCredentialProvider> _logger;

    public RegistryCredentialProvider(
        IRegistryRepository registryRepository,
        IOrganizationRepository organizationRepository,
        ILogger<RegistryCredentialProvider> logger)
    {
        _registryRepository = registryRepository;
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public RegistryCredentials? GetCredentialsForImage(string imageReference)
    {
        if (string.IsNullOrEmpty(imageReference))
        {
            return null;
        }

        try
        {
            // Get organization
            var organization = _organizationRepository.GetAll().FirstOrDefault();
            if (organization == null)
            {
                _logger.LogDebug("No organization found, cannot retrieve registry credentials");
                return null;
            }

            var organizationId = Domain.Deployment.OrganizationId.FromIdentityAccess(organization.Id);

            // Find matching registry
            var registry = _registryRepository.FindMatchingRegistry(organizationId, imageReference);

            if (registry == null)
            {
                _logger.LogDebug("No matching registry found for image {Image}", imageReference);
                return null;
            }

            if (!registry.HasCredentials)
            {
                _logger.LogDebug("Registry {RegistryName} found for image {Image}, but has no credentials configured",
                    registry.Name, imageReference);
                return null;
            }

            _logger.LogInformation("Using credentials from registry {RegistryName} for image {Image}",
                registry.Name, imageReference);

            return new RegistryCredentials(
                ServerAddress: registry.GetRegistryHost(),
                Username: registry.Username!,
                Password: registry.Password!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get registry credentials for image {Image}", imageReference);
            return null;
        }
    }
}
