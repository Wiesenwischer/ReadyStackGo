namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// SQLite-backed implementation of IOrganizationRepository.
/// </summary>
public class OrganizationRepository : IOrganizationRepository
{
    private readonly ReadyStackGoDbContext _context;

    public OrganizationRepository(ReadyStackGoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public OrganizationId NextIdentity() => OrganizationId.Create();

    public void Add(Organization organization)
    {
        _context.Organizations.Add(organization);
        _context.SaveChanges();
    }

    public Organization? Get(OrganizationId id)
    {
        return _context.Organizations.Find(id);
    }

    public Organization? GetByName(string name)
    {
        return _context.Organizations.FirstOrDefault(t => t.Name == name);
    }

    public IEnumerable<Organization> GetAll()
    {
        return _context.Organizations.ToList();
    }

    public void Remove(Organization organization)
    {
        _context.Organizations.Remove(organization);
        _context.SaveChanges();
    }
}
