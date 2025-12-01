namespace ReadyStackGo.Infrastructure.Persistence.Repositories;

using ReadyStackGo.Domain.IdentityAccess.Aggregates;
using ReadyStackGo.Domain.IdentityAccess.Repositories;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

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
