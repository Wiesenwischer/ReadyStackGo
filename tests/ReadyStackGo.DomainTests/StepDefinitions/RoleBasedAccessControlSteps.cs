namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

[Binding]
public class RoleBasedAccessControlSteps
{
    private readonly TestContext _context;

    public RoleBasedAccessControlSteps(TestContext context)
    {
        _context = context;
    }

    [Given(@"environment ""(.*)"" exists in ""(.*)""")]
    public void GivenEnvironmentExistsIn(string envName, string orgName)
    {
        var tenant = _context.Tenants.GetValueOrDefault(orgName);
        if (tenant == null)
        {
            var tenantId = _context.TenantRepository.NextIdentity();
            tenant = Tenant.Provision(tenantId, orgName, "Test organization");
            tenant.Activate();
            _context.TenantRepository.Add(tenant);
            _context.Tenants[orgName] = tenant;
        }

        // Store environment with a generated ID (in real impl, this would be an Environment aggregate)
        var envId = Guid.NewGuid();
        _context.Environments[envName] = envId;
    }

    [Given(@"user ""(.*)"" is SystemAdmin")]
    public void GivenUserIsSystemAdmin(string username)
    {
        var tenant = _context.Tenants.Values.FirstOrDefault()
            ?? throw new InvalidOperationException("No tenant exists");

        var user = CreateOrGetUser(username, tenant.Id);
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
    }

    [Given(@"user ""(.*)"" exists in ""(.*)""")]
    public void GivenUserExistsIn(string username, string orgName)
    {
        var tenant = _context.Tenants[orgName];
        CreateOrGetUser(username, tenant.Id);
    }

    [Given(@"user ""(.*)"" has role ""(.*)"" for environment ""(.*)""")]
    public void GivenUserHasRoleForEnvironment(string username, string roleName, string envName)
    {
        var user = _context.Users[username];
        var envId = _context.Environments[envName];
        var roleId = new RoleId(roleName);
        user.AssignRole(RoleAssignment.ForEnvironment(roleId, envId.ToString()));
    }

    [Given(@"user ""(.*)"" has role ""(.*)"" for organization ""(.*)""")]
    public void GivenUserHasRoleForOrganization(string username, string roleName, string orgName)
    {
        var user = _context.Users[username];
        var tenant = _context.Tenants[orgName];
        var roleId = new RoleId(roleName);
        user.AssignRole(RoleAssignment.ForOrganization(roleId, tenant.Id.Value.ToString()));
    }

    [Then(@"user ""(.*)"" should have permission ""(.*)"" with global scope")]
    public void ThenUserShouldHavePermissionWithGlobalScope(string username, string permissionString)
    {
        var user = _context.Users[username];
        var permission = Permission.Parse(permissionString);

        var hasPermission = CheckUserPermission(user, permission, ScopeType.Global, null);
        hasPermission.Should().BeTrue($"User '{username}' should have permission '{permissionString}' with global scope");
    }

    [Then(@"user ""(.*)"" should have permission ""(.*)"" for environment ""(.*)""")]
    public void ThenUserShouldHavePermissionForEnvironment(string username, string permissionString, string envName)
    {
        var user = _context.Users[username];
        var envId = _context.Environments[envName];
        var permission = Permission.Parse(permissionString);

        var hasPermission = CheckUserPermission(user, permission, ScopeType.Environment, envId.ToString());
        hasPermission.Should().BeTrue($"User '{username}' should have permission '{permissionString}' for environment '{envName}'");
    }

    [Then(@"user ""(.*)"" should have permission ""(.*)"" for organization ""(.*)""")]
    public void ThenUserShouldHavePermissionForOrganization(string username, string permissionString, string orgName)
    {
        var user = _context.Users[username];
        var tenant = _context.Tenants[orgName];
        var permission = Permission.Parse(permissionString);

        var hasPermission = CheckUserPermission(user, permission, ScopeType.Organization, tenant.Id.Value.ToString());
        hasPermission.Should().BeTrue($"User '{username}' should have permission '{permissionString}' for organization '{orgName}'");
    }

    [Then(@"user ""(.*)"" should not have permission ""(.*)"" with global scope")]
    public void ThenUserShouldNotHavePermissionWithGlobalScope(string username, string permissionString)
    {
        var user = _context.Users[username];
        var permission = Permission.Parse(permissionString);

        var hasPermission = CheckUserPermission(user, permission, ScopeType.Global, null);
        hasPermission.Should().BeFalse($"User '{username}' should NOT have permission '{permissionString}' with global scope");
    }

    [Then(@"user ""(.*)"" should not have permission ""(.*)"" for organization ""(.*)""")]
    public void ThenUserShouldNotHavePermissionForOrganization(string username, string permissionString, string orgName)
    {
        var user = _context.Users[username];
        var tenant = _context.Tenants[orgName];
        var permission = Permission.Parse(permissionString);

        var hasPermission = CheckUserPermission(user, permission, ScopeType.Organization, tenant.Id.Value.ToString());
        hasPermission.Should().BeFalse($"User '{username}' should NOT have permission '{permissionString}' for organization '{orgName}'");
    }

    private User CreateOrGetUser(string username, TenantId tenantId)
    {
        if (_context.Users.TryGetValue(username, out var existingUser))
        {
            return existingUser;
        }

        var userId = _context.UserRepository.NextIdentity();
        var email = new EmailAddress($"{username}@test.com");
        var password = HashedPassword.Create("TestPass123!", _context.PasswordHasher);
        var user = User.Register(userId, tenantId, username, email, password);
        _context.UserRepository.Add(user);
        _context.Users[username] = user;
        return user;
    }

    private bool CheckUserPermission(User user, Permission permission, ScopeType scopeType, string? scopeId)
    {
        // Check each role assignment
        foreach (var assignment in user.RoleAssignments)
        {
            var role = Role.GetById(assignment.RoleId);
            if (role == null) continue;

            // Global scope grants access to everything
            if (assignment.ScopeType == ScopeType.Global && role.HasPermission(permission))
            {
                return true;
            }

            // Exact scope match
            if (assignment.ScopeType == scopeType && assignment.ScopeId == scopeId && role.HasPermission(permission))
            {
                return true;
            }

            // Organization scope covers environments (hierarchical)
            if (assignment.ScopeType == ScopeType.Organization && scopeType == ScopeType.Environment)
            {
                // In real implementation, we'd check if the environment belongs to this organization
                // For testing, we assume all environments belong to the organization
                if (role.HasPermission(permission))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
