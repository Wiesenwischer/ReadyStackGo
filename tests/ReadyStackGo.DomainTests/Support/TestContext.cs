namespace ReadyStackGo.DomainTests.Support;

using ReadyStackGo.Domain.IdentityAccess.Aggregates;
using ReadyStackGo.Domain.IdentityAccess.Services;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;
using ReadyStackGo.Domain.IdentityAccess.Aggregates;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

/// <summary>
/// Shared test context for Reqnroll scenarios.
/// </summary>
public class TestContext
{
    public InMemoryTenantRepository TenantRepository { get; } = new();
    public InMemoryUserRepository UserRepository { get; } = new();
    public IPasswordHasher PasswordHasher { get; } = new TestPasswordHasher();

    public TenantProvisioningService TenantProvisioningService => new(
        TenantRepository,
        UserRepository,
        PasswordHasher);

    public AuthenticationService AuthenticationService => new(
        UserRepository,
        TenantRepository,
        PasswordHasher);

    // Current state for scenarios
    public Tenant? CurrentTenant { get; set; }
    public User? CurrentUser { get; set; }
    public User? AuthenticatedUser { get; set; }
    public Exception? LastException { get; set; }
    public bool AuthenticationSucceeded { get; set; }

    // Track created entities
    public Dictionary<string, Tenant> Tenants { get; } = new();
    public Dictionary<string, User> Users { get; } = new();
    public Dictionary<string, Guid> Environments { get; } = new();

    public void Reset()
    {
        TenantRepository.Clear();
        UserRepository.Clear();
        CurrentTenant = null;
        CurrentUser = null;
        AuthenticatedUser = null;
        LastException = null;
        AuthenticationSucceeded = false;
        Tenants.Clear();
        Users.Clear();
        Environments.Clear();
    }

    public Role GetRole(string roleName)
    {
        var roleId = new RoleId(roleName);
        return Role.GetById(roleId)
            ?? throw new InvalidOperationException($"Role '{roleName}' not found");
    }
}
