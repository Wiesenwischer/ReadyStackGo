namespace ReadyStackGo.Domain.IdentityAccess.Repositories;

using ReadyStackGo.Domain.IdentityAccess.Aggregates;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

/// <summary>
/// Repository interface for Role aggregate.
/// </summary>
public interface IRoleRepository
{
    Role? Get(RoleId id);
    IEnumerable<Role> GetAll();
}
