namespace ReadyStackGo.Domain.IdentityAccess.Repositories;

using ReadyStackGo.Domain.IdentityAccess.Aggregates;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

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
