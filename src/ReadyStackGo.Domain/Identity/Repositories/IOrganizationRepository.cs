namespace ReadyStackGo.Domain.Identity.Repositories;

using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.ValueObjects;

/// <summary>
/// Repository interface for Organization aggregate.
/// </summary>
public interface IOrganizationRepository
{
    OrganizationId NextIdentity();
    void Add(Organization organization);
    Organization? Get(OrganizationId id);
    Organization? GetByName(string name);
    IEnumerable<Organization> GetAll();
    void Remove(Organization organization);
}
