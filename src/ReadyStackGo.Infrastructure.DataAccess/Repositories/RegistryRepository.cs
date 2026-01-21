namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Registries;

/// <summary>
/// SQLite implementation of IRegistryRepository.
/// </summary>
public class RegistryRepository : IRegistryRepository
{
    private readonly ReadyStackGoDbContext _context;

    public RegistryRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public Registry? GetById(RegistryId id)
    {
        return _context.Registries
            .FirstOrDefault(r => r.Id == id);
    }

    public IEnumerable<Registry> GetByOrganization(OrganizationId organizationId)
    {
        return _context.Registries
            .Where(r => r.OrganizationId == organizationId)
            .OrderBy(r => r.Name)
            .ToList();
    }

    public Registry? GetDefault(OrganizationId organizationId)
    {
        return _context.Registries
            .FirstOrDefault(r => r.OrganizationId == organizationId && r.IsDefault);
    }

    public Registry? FindMatchingRegistry(OrganizationId organizationId, string imageReference)
    {
        var registries = _context.Registries
            .Where(r => r.OrganizationId == organizationId)
            .ToList();

        // First try to find a matching registry
        var matchingRegistry = registries.FirstOrDefault(r => r.MatchesImage(imageReference));
        if (matchingRegistry != null)
            return matchingRegistry;

        // Fall back to default registry if no specific match
        return registries.FirstOrDefault(r => r.IsDefault);
    }

    public void Add(Registry registry)
    {
        _context.Registries.Add(registry);
    }

    public void Update(Registry registry)
    {
        _context.Registries.Update(registry);
    }

    public void Remove(Registry registry)
    {
        _context.Registries.Remove(registry);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
