namespace ReadyStackGo.DomainTests.Support;

using ReadyStackGo.Domain.IdentityAccess.Organizations;

/// <summary>
/// In-memory implementation of IOrganizationRepository for testing.
/// </summary>
public class InMemoryOrganizationRepository : IOrganizationRepository
{
    private readonly Dictionary<OrganizationId, Organization> _organizations = new();

    public OrganizationId NextIdentity() => OrganizationId.Create();

    public void Add(Organization organization)
    {
        _organizations[organization.Id] = organization;
    }

    public Organization? Get(OrganizationId id)
    {
        return _organizations.GetValueOrDefault(id);
    }

    public Organization? GetByName(string name)
    {
        return _organizations.Values.FirstOrDefault(o => o.Name == name);
    }

    public IEnumerable<Organization> GetAll()
    {
        return _organizations.Values;
    }

    public void Remove(Organization organization)
    {
        _organizations.Remove(organization.Id);
    }

    public void Clear()
    {
        _organizations.Clear();
    }
}
