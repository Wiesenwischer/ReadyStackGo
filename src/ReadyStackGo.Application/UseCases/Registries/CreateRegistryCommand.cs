namespace ReadyStackGo.Application.UseCases.Registries;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

/// <summary>
/// Command to create a new registry.
/// </summary>
public record CreateRegistryCommand(CreateRegistryRequest Request) : IRequest<RegistryResponse>;

public class CreateRegistryHandler : IRequestHandler<CreateRegistryCommand, RegistryResponse>
{
    private readonly IRegistryRepository _registryRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<CreateRegistryHandler> _logger;

    public CreateRegistryHandler(
        IRegistryRepository registryRepository,
        IOrganizationRepository organizationRepository,
        ILogger<CreateRegistryHandler> logger)
    {
        _registryRepository = registryRepository;
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public Task<RegistryResponse> Handle(CreateRegistryCommand command, CancellationToken cancellationToken)
    {
        var organization = _organizationRepository.GetAll().FirstOrDefault();
        if (organization == null)
        {
            return Task.FromResult(new RegistryResponse(false, "Organization not set. Complete the setup wizard first."));
        }

        var organizationId = DeploymentOrganizationId.FromIdentityAccess(organization.Id);
        var request = command.Request;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Task.FromResult(new RegistryResponse(false, "Registry name is required"));
        }

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return Task.FromResult(new RegistryResponse(false, "Registry URL is required"));
        }

        try
        {
            var registryId = RegistryId.Create();
            var registry = Registry.Create(
                registryId,
                organizationId,
                request.Name,
                request.Url,
                request.Username,
                request.Password);

            // Set image patterns if provided
            if (request.ImagePatterns != null && request.ImagePatterns.Count > 0)
            {
                registry.SetImagePatterns(request.ImagePatterns);
            }

            _registryRepository.Add(registry);
            _registryRepository.SaveChanges();

            _logger.LogInformation("Created registry {RegistryId} '{Name}' for organization {OrganizationId}",
                registryId, request.Name, organizationId);

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

            return Task.FromResult(new RegistryResponse(true, "Registry created successfully", dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create registry '{Name}'", request.Name);
            return Task.FromResult(new RegistryResponse(false, $"Failed to create registry: {ex.Message}"));
        }
    }
}
