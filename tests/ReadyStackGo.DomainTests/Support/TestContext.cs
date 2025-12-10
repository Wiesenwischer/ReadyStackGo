namespace ReadyStackGo.DomainTests.Support;

using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Deployments;
using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

/// <summary>
/// Shared test context for Reqnroll scenarios.
/// </summary>
public class TestContext
{
    public InMemoryOrganizationRepository OrganizationRepository { get; } = new();
    public InMemoryUserRepository UserRepository { get; } = new();
    public InMemoryEnvironmentRepository EnvironmentRepository { get; } = new();
    public IPasswordHasher PasswordHasher { get; } = new TestPasswordHasher();

    public OrganizationProvisioningService OrganizationProvisioningService => new(
        OrganizationRepository,
        UserRepository);

    public AuthenticationService AuthenticationService => new(
        UserRepository,
        PasswordHasher);

    // Current state for scenarios
    public Organization? CurrentOrganization { get; set; }
    public User? CurrentUser { get; set; }
    public User? AuthenticatedUser { get; set; }
    public Environment? CurrentEnvironment { get; set; }
    public Deployment? CurrentDeployment { get; set; }
    public Exception? LastException { get; set; }
    public bool AuthenticationSucceeded { get; set; }
    public bool SystemInitialized { get; set; }

    // Track created entities
    public Dictionary<string, Organization> Organizations { get; } = new();
    public Dictionary<string, User> Users { get; } = new();
    public Dictionary<string, Guid> Environments { get; } = new();
    public Dictionary<string, Environment> EnvironmentEntities { get; } = new();
    public Dictionary<string, Deployment> Deployments { get; } = new();

    public void Reset()
    {
        OrganizationRepository.Clear();
        UserRepository.Clear();
        EnvironmentRepository.Clear();
        CurrentOrganization = null;
        CurrentUser = null;
        AuthenticatedUser = null;
        CurrentEnvironment = null;
        CurrentDeployment = null;
        LastException = null;
        AuthenticationSucceeded = false;
        SystemInitialized = false;
        Organizations.Clear();
        Users.Clear();
        Environments.Clear();
        EnvironmentEntities.Clear();
        Deployments.Clear();
    }

    public Role GetRole(string roleName)
    {
        var roleId = new RoleId(roleName);
        return Role.GetById(roleId)
            ?? throw new InvalidOperationException($"Role '{roleName}' not found");
    }
}
