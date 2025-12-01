namespace ReadyStackGo.DomainTests.Support;

using ReadyStackGo.Domain.IdentityAccess.Aggregates;
using ReadyStackGo.Domain.IdentityAccess.Repositories;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

/// <summary>
/// In-memory implementation of ITenantRepository for testing.
/// </summary>
public class InMemoryTenantRepository : ITenantRepository
{
    private readonly Dictionary<TenantId, Tenant> _tenants = new();

    public TenantId NextIdentity() => TenantId.Create();

    public void Add(Tenant tenant)
    {
        _tenants[tenant.Id] = tenant;
    }

    public Tenant? Get(TenantId id)
    {
        return _tenants.GetValueOrDefault(id);
    }

    public Tenant? GetByName(string name)
    {
        return _tenants.Values.FirstOrDefault(t => t.Name == name);
    }

    public IEnumerable<Tenant> GetAll()
    {
        return _tenants.Values;
    }

    public void Remove(Tenant tenant)
    {
        _tenants.Remove(tenant.Id);
    }

    public void Clear()
    {
        _tenants.Clear();
    }
}
