namespace ReadyStackGo.Infrastructure.Persistence.Repositories;

using ReadyStackGo.Domain.Access.Aggregates;
using ReadyStackGo.Domain.Access.Repositories;
using ReadyStackGo.Domain.Access.ValueObjects;

/// <summary>
/// In-memory implementation of IRoleRepository.
/// Roles are predefined and not persisted to the database.
/// </summary>
public class RoleRepository : IRoleRepository
{
    public Role? Get(RoleId id)
    {
        return Role.GetById(id);
    }

    public IEnumerable<Role> GetAll()
    {
        return Role.GetAll();
    }
}
