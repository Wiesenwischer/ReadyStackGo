namespace ReadyStackGo.Domain.Identity.Repositories;

using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.ValueObjects;

/// <summary>
/// Repository interface for Tenant aggregate.
/// </summary>
public interface ITenantRepository
{
    TenantId NextIdentity();
    void Add(Tenant tenant);
    Tenant? Get(TenantId id);
    Tenant? GetByName(string name);
    IEnumerable<Tenant> GetAll();
    void Remove(Tenant tenant);
}
