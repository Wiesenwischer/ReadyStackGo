namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// SQLite implementation of IEnvironmentVariableRepository.
/// </summary>
public class EnvironmentVariableRepository : IEnvironmentVariableRepository
{
    private readonly ReadyStackGoDbContext _context;

    public EnvironmentVariableRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public EnvironmentVariableId NextIdentity()
    {
        return EnvironmentVariableId.Create();
    }

    public void Add(EnvironmentVariable environmentVariable)
    {
        _context.EnvironmentVariables.Add(environmentVariable);
    }

    public void Update(EnvironmentVariable environmentVariable)
    {
        _context.EnvironmentVariables.Update(environmentVariable);
    }

    public EnvironmentVariable? Get(EnvironmentVariableId id)
    {
        return _context.EnvironmentVariables
            .FirstOrDefault(ev => ev.Id == id);
    }

    public EnvironmentVariable? GetByEnvironmentAndKey(EnvironmentId environmentId, string key)
    {
        return _context.EnvironmentVariables
            .FirstOrDefault(ev => ev.EnvironmentId == environmentId && ev.Key == key);
    }

    public IEnumerable<EnvironmentVariable> GetByEnvironment(EnvironmentId environmentId)
    {
        return _context.EnvironmentVariables
            .Where(ev => ev.EnvironmentId == environmentId)
            .OrderBy(ev => ev.Key)
            .ToList();
    }

    public void Remove(EnvironmentVariable environmentVariable)
    {
        _context.EnvironmentVariables.Remove(environmentVariable);
    }

    public void RemoveAllByEnvironment(EnvironmentId environmentId)
    {
        var variables = _context.EnvironmentVariables
            .Where(ev => ev.EnvironmentId == environmentId)
            .ToList();

        _context.EnvironmentVariables.RemoveRange(variables);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}
