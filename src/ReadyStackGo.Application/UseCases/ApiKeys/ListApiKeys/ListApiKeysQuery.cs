using MediatR;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.Application.UseCases.ApiKeys.ListApiKeys;

public record ListApiKeysQuery() : IRequest<ListApiKeysResponse>;

public class ListApiKeysHandler : IRequestHandler<ListApiKeysQuery, ListApiKeysResponse>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public ListApiKeysHandler(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _organizationRepository = organizationRepository;
    }

    public Task<ListApiKeysResponse> Handle(ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        var organization = _organizationRepository.GetAll().FirstOrDefault();
        if (organization == null)
        {
            return Task.FromResult(new ListApiKeysResponse(Array.Empty<ApiKeyDto>()));
        }

        var apiKeys = _apiKeyRepository.GetByOrganization(organization.Id);

        var dtos = apiKeys.Select(k => new ApiKeyDto(
            Id: k.Id.Value.ToString(),
            Name: k.Name,
            KeyPrefix: k.KeyPrefix,
            OrganizationId: k.OrganizationId.Value.ToString(),
            EnvironmentId: k.EnvironmentId?.ToString(),
            Permissions: k.Permissions,
            CreatedAt: k.CreatedAt,
            LastUsedAt: k.LastUsedAt,
            ExpiresAt: k.ExpiresAt,
            IsRevoked: k.IsRevoked
        )).ToList();

        return Task.FromResult(new ListApiKeysResponse(dtos));
    }
}
