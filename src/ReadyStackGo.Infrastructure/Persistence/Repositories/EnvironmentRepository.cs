namespace ReadyStackGo.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.StackManagement.Aggregates;
using ReadyStackGo.Domain.StackManagement.Repositories;
using ReadyStackGo.Domain.StackManagement.ValueObjects;

/// <summary>
/// SQLite implementation of IEnvironmentRepository.
/// </summary>
public class EnvironmentRepository : IEnvironmentRepository
{
    private readonly ReadyStackGoDbContext _context;

    public EnvironmentRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public EnvironmentId NextIdentity()
    {
        return EnvironmentId.Create();
    }

    public void Add(Environment environment)
    {
        _context.Environments.Add(environment);
    }

    public void Update(Environment environment)
    {
        _context.Environments.Update(environment);
    }

    public Environment? Get(EnvironmentId id)
    {
        return _context.Environments
            .FirstOrDefault(e => e.Id == id);
    }

    public Environment? GetByName(OrganizationId organizationId, string name)
    {
        return _context.Environments
            .FirstOrDefault(e => e.OrganizationId == organizationId && e.Name == name);
    }

    public IEnumerable<Environment> GetByOrganization(OrganizationId organizationId)
    {
        return _context.Environments
            .Where(e => e.OrganizationId == organizationId)
            .OrderBy(e => e.Name)
            .ToList();
    }

    public Environment? GetDefault(OrganizationId organizationId)
    {
        return _context.Environments
            .FirstOrDefault(e => e.OrganizationId == organizationId && e.IsDefault);
    }

    public void Remove(Environment environment)
    {
        _context.Environments.Remove(environment);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
