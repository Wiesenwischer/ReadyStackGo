namespace ReadyStackGo.Domain.Access.Repositories;

using ReadyStackGo.Domain.Access.Aggregates;
using ReadyStackGo.Domain.Access.ValueObjects;

/// <summary>
/// Repository interface for Role aggregate.
/// </summary>
public interface IRoleRepository
{
    Role? Get(RoleId id);
    IEnumerable<Role> GetAll();
}
