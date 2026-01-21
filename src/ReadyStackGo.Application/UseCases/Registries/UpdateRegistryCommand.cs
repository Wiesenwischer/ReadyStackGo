namespace ReadyStackGo.Application.UseCases.Registries;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Registries;

/// <summary>
/// Command to update an existing registry.
/// </summary>
public record UpdateRegistryCommand(
    string RegistryId,
    UpdateRegistryRequest Request) : IRequest<RegistryResponse>;

public class UpdateRegistryHandler : IRequestHandler<UpdateRegistryCommand, RegistryResponse>
{
    private readonly IRegistryRepository _registryRepository;
    private readonly ILogger<UpdateRegistryHandler> _logger;

    public UpdateRegistryHandler(
        IRegistryRepository registryRepository,
        ILogger<UpdateRegistryHandler> logger)
    {
        _registryRepository = registryRepository;
        _logger = logger;
    }

    public Task<RegistryResponse> Handle(UpdateRegistryCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.RegistryId, out var registryGuid))
        {
            return Task.FromResult(new RegistryResponse(false, "Invalid registry ID format"));
        }

        var registry = _registryRepository.GetById(new RegistryId(registryGuid));
        if (registry == null)
        {
            return Task.FromResult(new RegistryResponse(false, "Registry not found"));
        }

        var request = command.Request;

        try
        {
            // Update name if provided
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                registry.UpdateName(request.Name);
            }

            // Update URL if provided
            if (!string.IsNullOrWhiteSpace(request.Url))
            {
                registry.UpdateUrl(request.Url);
            }

            // Update credentials
            if (request.ClearCredentials == true)
            {
                registry.UpdateCredentials(null, null);
            }
            else if (request.Username != null || request.Password != null)
            {
                // Only update if at least one credential field is provided
                var newUsername = request.Username ?? registry.Username;
                var newPassword = request.Password ?? registry.Password;
                registry.UpdateCredentials(newUsername, newPassword);
            }

            // Update image patterns if provided
            if (request.ImagePatterns != null)
            {
                registry.SetImagePatterns(request.ImagePatterns);
            }

            _registryRepository.Update(registry);
            _registryRepository.SaveChanges();

            _logger.LogInformation("Updated registry {RegistryId} '{Name}'",
                registry.Id, registry.Name);

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

            return Task.FromResult(new RegistryResponse(true, "Registry updated successfully", dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update registry {RegistryId}", command.RegistryId);
            return Task.FromResult(new RegistryResponse(false, $"Failed to update registry: {ex.Message}"));
        }
    }
}
