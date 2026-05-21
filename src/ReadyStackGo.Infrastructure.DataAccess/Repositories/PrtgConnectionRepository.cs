namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.PrtgConnections;

public class PrtgConnectionRepository : IPrtgConnectionRepository
{
    private readonly ReadyStackGoDbContext _context;

    public PrtgConnectionRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public PrtgConnection? Get(PrtgConnectionId id)
        => _context.PrtgConnections.FirstOrDefault(c => c.Id == id);

    public IReadOnlyList<PrtgConnection> GetByOrganization(OrganizationId organizationId)
        => _context.PrtgConnections
            .Where(c => c.OrganizationId == organizationId)
            .OrderBy(c => c.Name)
            .ToList();

    public PrtgConnection? GetByName(OrganizationId organizationId, string name)
        => _context.PrtgConnections
            .FirstOrDefault(c => c.OrganizationId == organizationId && c.Name == name);

    public void Add(PrtgConnection connection)
        => _context.PrtgConnections.Add(connection);

    public void Update(PrtgConnection connection)
        => _context.PrtgConnections.Update(connection);

    public void Delete(PrtgConnection connection)
        => _context.PrtgConnections.Remove(connection);

    public void SaveChanges()
        => _context.SaveChanges();
}
