namespace ReadyStackGo.Application.UseCases.Registries;

using MediatR;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

/// <summary>
/// Query to list all registries for the organization.
/// </summary>
public record ListRegistriesQuery() : IRequest<ListRegistriesResponse>;

public class ListRegistriesHandler : IRequestHandler<ListRegistriesQuery, ListRegistriesResponse>
{
    private readonly IRegistryRepository _registryRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public ListRegistriesHandler(
        IRegistryRepository registryRepository,
        IOrganizationRepository organizationRepository)
    {
        _registryRepository = registryRepository;
        _organizationRepository = organizationRepository;
    }

    public Task<ListRegistriesResponse> Handle(ListRegistriesQuery request, CancellationToken cancellationToken)
    {
        var organization = _organizationRepository.GetAll().FirstOrDefault();
        if (organization == null)
        {
            return Task.FromResult(new ListRegistriesResponse(Array.Empty<RegistryDto>()));
        }

        var organizationId = DeploymentOrganizationId.FromIdentityAccess(organization.Id);
        var registries = _registryRepository.GetByOrganization(organizationId);

        var dtos = registries.Select(r => new RegistryDto(
            Id: r.Id.Value.ToString(),
            Name: r.Name,
            Url: r.Url,
            Username: r.Username,
            HasCredentials: r.HasCredentials,
            IsDefault: r.IsDefault,
            ImagePatterns: r.ImagePatterns,
            CreatedAt: r.CreatedAt,
            UpdatedAt: r.UpdatedAt
        )).ToList();

        return Task.FromResult(new ListRegistriesResponse(dtos));
    }
}
