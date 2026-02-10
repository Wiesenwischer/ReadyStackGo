namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

/// <summary>
/// SQLite implementation of IApiKeyRepository.
/// </summary>
public class ApiKeyRepository : IApiKeyRepository
{
    private readonly ReadyStackGoDbContext _context;

    public ApiKeyRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public ApiKey? GetById(ApiKeyId id)
    {
        return _context.ApiKeys
            .FirstOrDefault(a => a.Id == id);
    }

    public ApiKey? GetByKeyHash(string keyHash)
    {
        return _context.ApiKeys
            .FirstOrDefault(a => a.KeyHash == keyHash);
    }

    public IEnumerable<ApiKey> GetByOrganization(OrganizationId organizationId)
    {
        return _context.ApiKeys
            .Where(a => a.OrganizationId == organizationId)
            .OrderBy(a => a.Name)
            .ToList();
    }

    public void Add(ApiKey apiKey)
    {
        _context.ApiKeys.Add(apiKey);
    }

    public void Update(ApiKey apiKey)
    {
        _context.ApiKeys.Update(apiKey);
    }

    public void Remove(ApiKey apiKey)
    {
        _context.ApiKeys.Remove(apiKey);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
