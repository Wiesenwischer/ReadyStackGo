namespace ReadyStackGo.Application.UseCases.Registries;

using MediatR;
using ReadyStackGo.Domain.Deployment.Registries;

/// <summary>
/// Query to get a specific registry by ID.
/// </summary>
public record GetRegistryQuery(string RegistryId) : IRequest<RegistryResponse>;

public class GetRegistryHandler : IRequestHandler<GetRegistryQuery, RegistryResponse>
{
    private readonly IRegistryRepository _registryRepository;

    public GetRegistryHandler(IRegistryRepository registryRepository)
    {
        _registryRepository = registryRepository;
    }

    public Task<RegistryResponse> Handle(GetRegistryQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.RegistryId, out var registryGuid))
        {
            return Task.FromResult(new RegistryResponse(false, "Invalid registry ID format"));
        }

        var registry = _registryRepository.GetById(new RegistryId(registryGuid));
        if (registry == null)
        {
            return Task.FromResult(new RegistryResponse(false, "Registry not found"));
        }

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

        return Task.FromResult(new RegistryResponse(true, Registry: dto));
    }
}
