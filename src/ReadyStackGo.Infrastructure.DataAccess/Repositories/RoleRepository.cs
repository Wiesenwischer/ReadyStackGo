namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

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
