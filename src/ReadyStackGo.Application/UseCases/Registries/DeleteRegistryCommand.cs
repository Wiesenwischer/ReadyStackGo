namespace ReadyStackGo.Application.UseCases.Registries;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Registries;

/// <summary>
/// Command to delete a registry.
/// </summary>
public record DeleteRegistryCommand(string RegistryId) : IRequest<RegistryResponse>;

public class DeleteRegistryHandler : IRequestHandler<DeleteRegistryCommand, RegistryResponse>
{
    private readonly IRegistryRepository _registryRepository;
    private readonly ILogger<DeleteRegistryHandler> _logger;

    public DeleteRegistryHandler(
        IRegistryRepository registryRepository,
        ILogger<DeleteRegistryHandler> logger)
    {
        _registryRepository = registryRepository;
        _logger = logger;
    }

    public Task<RegistryResponse> Handle(DeleteRegistryCommand command, CancellationToken cancellationToken)
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

        try
        {
            var registryName = registry.Name;
            _registryRepository.Remove(registry);
            _registryRepository.SaveChanges();

            _logger.LogInformation("Deleted registry {RegistryId} '{Name}'",
                command.RegistryId, registryName);

            return Task.FromResult(new RegistryResponse(true, $"Registry '{registryName}' deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete registry {RegistryId}", command.RegistryId);
            return Task.FromResult(new RegistryResponse(false, $"Failed to delete registry: {ex.Message}"));
        }
    }
}
