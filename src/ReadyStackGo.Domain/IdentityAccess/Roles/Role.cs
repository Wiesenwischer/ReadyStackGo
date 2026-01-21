namespace ReadyStackGo.Domain.IdentityAccess.Roles;

using ReadyStackGo.Domain.SharedKernel;


/// <summary>
/// Aggregate root representing a role with permissions.
/// Roles are predefined and not user-creatable.
/// </summary>
public class Role : AggregateRoot<RoleId>
{
    private readonly List<Permission> _permissions = new();

    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public ScopeType AllowedScopes { get; private set; }

    public IReadOnlyCollection<Permission> Permissions => _permissions.AsReadOnly();

    // For EF Core
    protected Role() { }

    private Role(RoleId id, string name, string description, ScopeType allowedScopes, IEnumerable<Permission> permissions)
    {
        Id = id;
        Name = name;
        Description = description;
        AllowedScopes = allowedScopes;
        _permissions.AddRange(permissions);
    }

    public bool CanBeAssignedToScope(ScopeType scopeType)
    {
        return (AllowedScopes & scopeType) == scopeType;
    }

    public bool HasPermission(Permission permission)
    {
        return _permissions.Any(p => p.Includes(permission));
    }

    // Predefined roles
    public static Role SystemAdmin => new(
        RoleId.SystemAdmin,
        "System Administrator",
        "Full system access - can manage all organizations and users",
        ScopeType.Global,
        new[]
        {
            new Permission("*", "*") // All permissions
        });

    public static Role OrganizationOwner => new(
        RoleId.OrganizationOwner,
        "Organization Owner",
        "Full access to organization resources",
        ScopeType.Organization,
        new[]
        {
            Permission.Users.Create,
            Permission.Users.Read,
            Permission.Users.Update,
            Permission.Users.Delete,
            Permission.Environments.Create,
            Permission.Environments.Read,
            Permission.Environments.Update,
            Permission.Environments.Delete,
            Permission.Deployments.Create,
            Permission.Deployments.Read,
            Permission.Deployments.Update,
            Permission.Deployments.Delete,
            Permission.StackSources.Create,
            Permission.StackSources.Read,
            Permission.StackSources.Update,
            Permission.StackSources.Delete,
            Permission.Registries.Create,
            Permission.Registries.Read,
            Permission.Registries.Update,
            Permission.Registries.Delete,
            Permission.Stacks.Read,
            Permission.Dashboard.Read,
        });

    public static Role Operator => new(
        RoleId.Operator,
        "Operator",
        "Can deploy and manage stacks in assigned scope",
        ScopeType.Organization | ScopeType.Environment,
        new[]
        {
            Permission.Deployments.Create,
            Permission.Deployments.Read,
            Permission.Deployments.Update,
            Permission.Deployments.Delete,
            Permission.Environments.Read,
            Permission.StackSources.Read,
            Permission.Registries.Read,
            Permission.Stacks.Read,
            Permission.Dashboard.Read,
        });

    public static Role Viewer => new(
        RoleId.Viewer,
        "Viewer",
        "Read-only access to deployments and environments",
        ScopeType.Organization | ScopeType.Environment,
        new[]
        {
            Permission.Deployments.Read,
            Permission.Environments.Read,
            Permission.StackSources.Read,
            Permission.Stacks.Read,
            Permission.Dashboard.Read,
        });

    public static IEnumerable<Role> GetAll()
    {
        yield return SystemAdmin;
        yield return OrganizationOwner;
        yield return Operator;
        yield return Viewer;
    }

    public static Role? GetById(RoleId roleId)
    {
        return GetAll().FirstOrDefault(r => r.Id == roleId);
    }

    public override string ToString() =>
        $"Role [id={Id}, name={Name}]";
}
