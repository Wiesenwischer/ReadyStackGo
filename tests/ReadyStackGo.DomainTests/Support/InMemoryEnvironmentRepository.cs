namespace ReadyStackGo.DomainTests.Support;

using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

public class InMemoryEnvironmentRepository : IEnvironmentRepository
{
    private readonly Dictionary<EnvironmentId, Environment> _environments = new();

    public EnvironmentId NextIdentity() => EnvironmentId.Create();

    public void Add(Environment environment)
    {
        _environments[environment.Id] = environment;
    }

    public Environment? Get(EnvironmentId id)
    {
        return _environments.GetValueOrDefault(id);
    }

    public Environment? GetByName(OrganizationId organizationId, string name)
    {
        return _environments.Values.FirstOrDefault(e =>
            e.OrganizationId == organizationId && e.Name == name);
    }

    public IEnumerable<Environment> GetByOrganization(OrganizationId organizationId)
    {
        return _environments.Values.Where(e => e.OrganizationId == organizationId);
    }

    public Environment? GetDefault(OrganizationId organizationId)
    {
        return _environments.Values.FirstOrDefault(e =>
            e.OrganizationId == organizationId && e.IsDefault);
    }

    public void Update(Environment environment)
    {
        _environments[environment.Id] = environment;
    }

    public void Remove(Environment environment)
    {
        _environments.Remove(environment.Id);
    }

    public void Clear()
    {
        _environments.Clear();
    }

    public void SaveChanges()
    {
        // No-op for in-memory repository
    }
}
