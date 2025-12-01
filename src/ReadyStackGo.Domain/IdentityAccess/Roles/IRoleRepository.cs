namespace ReadyStackGo.Domain.IdentityAccess.Roles;




/// <summary>
/// Repository interface for Role aggregate.
/// </summary>
public interface IRoleRepository
{
    Role? Get(RoleId id);
    IEnumerable<Role> GetAll();
}
