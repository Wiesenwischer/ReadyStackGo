namespace ReadyStackGo.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.Repositories;
using ReadyStackGo.Domain.Identity.ValueObjects;

/// <summary>
/// SQLite-backed implementation of ITenantRepository.
/// </summary>
public class TenantRepository : ITenantRepository
{
    private readonly ReadyStackGoDbContext _context;

    public TenantRepository(ReadyStackGoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public TenantId NextIdentity() => TenantId.Create();

    public void Add(Tenant tenant)
    {
        _context.Tenants.Add(tenant);
        _context.SaveChanges();
    }

    public Tenant? Get(TenantId id)
    {
        return _context.Tenants.Find(id);
    }

    public Tenant? GetByName(string name)
    {
        return _context.Tenants.FirstOrDefault(t => t.Name == name);
    }

    public IEnumerable<Tenant> GetAll()
    {
        return _context.Tenants.ToList();
    }

    public void Remove(Tenant tenant)
    {
        _context.Tenants.Remove(tenant);
        _context.SaveChanges();
    }
}
