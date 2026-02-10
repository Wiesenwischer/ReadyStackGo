using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.UnitTests.Authentication;

public class RbacServiceApiKeyTests
{
    private readonly RbacService _rbacService;

    public RbacServiceApiKeyTests()
    {
        _rbacService = new RbacService();
    }

    private ClaimsPrincipal CreateApiKeyUser(string orgId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(RbacClaimTypes.UserId, $"apikey:{Guid.NewGuid()}"),
            new(RbacClaimTypes.ApiKeyId, Guid.NewGuid().ToString()),
            new(RbacClaimTypes.ApiKeyName, "Test API Key")
        };

        // Add Operator role at org scope (like the auth handler does)
        var roleAssignments = new List<RoleAssignmentClaim>
        {
            new()
            {
                Role = RoleId.Operator.Value,
                Scope = ScopeType.Organization.ToString(),
                ScopeId = orgId
            }
        };
        claims.Add(new Claim(RbacClaimTypes.RoleAssignments, JsonSerializer.Serialize(roleAssignments)));

        // Add API permissions
        foreach (var permission in permissions)
        {
            claims.Add(new Claim(RbacClaimTypes.ApiPermission, permission));
        }

        var identity = new ClaimsIdentity(claims, "ApiKey");
        return new ClaimsPrincipal(identity);
    }

    private ClaimsPrincipal CreateApiKeyUserWithoutRole(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(RbacClaimTypes.UserId, $"apikey:{Guid.NewGuid()}"),
            new(RbacClaimTypes.ApiKeyId, Guid.NewGuid().ToString()),
            new(RbacClaimTypes.ApiKeyName, "Test API Key"),
            new(RbacClaimTypes.RoleAssignments, "[]")
        };

        foreach (var permission in permissions)
        {
            claims.Add(new Claim(RbacClaimTypes.ApiPermission, permission));
        }

        var identity = new ClaimsIdentity(claims, "ApiKey");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void HasPermission_WithExactApiPermission_ReturnsTrue()
    {
        var user = CreateApiKeyUserWithoutRole("Hooks.Redeploy");

        var result = _rbacService.HasPermission(user, Permission.Hooks.Redeploy);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WithWildcardAll_ReturnsTrue()
    {
        var user = CreateApiKeyUserWithoutRole("*.*");

        var result = _rbacService.HasPermission(user, Permission.Hooks.Redeploy);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WithResourceWildcard_ReturnsTrue()
    {
        var user = CreateApiKeyUserWithoutRole("Hooks.*");

        var result = _rbacService.HasPermission(user, Permission.Hooks.Redeploy);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WithResourceWildcard_MatchesAllActionsInResource()
    {
        var user = CreateApiKeyUserWithoutRole("Hooks.*");

        _rbacService.HasPermission(user, Permission.Hooks.Redeploy).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Hooks.Upgrade).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Hooks.SyncSources).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WithResourceWildcard_DoesNotMatchOtherResources()
    {
        var user = CreateApiKeyUserWithoutRole("Hooks.*");

        _rbacService.HasPermission(user, Permission.Deployments.Create).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WithoutMatchingApiPermission_ReturnsFalse()
    {
        var user = CreateApiKeyUserWithoutRole("Hooks.Redeploy");

        var result = _rbacService.HasPermission(user, Permission.Hooks.Upgrade);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WithNoApiPermissions_ReturnsFalse()
    {
        var user = CreateApiKeyUserWithoutRole();

        var result = _rbacService.HasPermission(user, Permission.Hooks.Redeploy);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_RoleBasedCheckTakesPrecedence()
    {
        // API key with Operator role at org scope + explicit Hooks.Redeploy permission
        var orgId = Guid.NewGuid().ToString();
        var user = CreateApiKeyUser(orgId, "Hooks.Redeploy");

        // Operator role has Deployments.Create — check passes via role
        _rbacService.HasPermission(user, Permission.Deployments.Create, orgId).Should().BeTrue();

        // Hooks.Redeploy is NOT in Operator role but IS in api_permission — check passes via fallback
        _rbacService.HasPermission(user, Permission.Hooks.Redeploy).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_MultipleApiPermissions_AllWork()
    {
        var user = CreateApiKeyUserWithoutRole("Hooks.Redeploy", "Hooks.Upgrade", "Hooks.SyncSources");

        _rbacService.HasPermission(user, Permission.Hooks.Redeploy).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Hooks.Upgrade).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Hooks.SyncSources).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ApiPermissionDoesNotBypassScopeForRoles()
    {
        // API key has Operator role at org1 scope
        var orgId1 = Guid.NewGuid().ToString();
        var orgId2 = Guid.NewGuid().ToString();
        var user = CreateApiKeyUser(orgId1, "Hooks.Redeploy");

        // Role-based check for org1 passes
        _rbacService.HasPermission(user, Permission.Deployments.Create, orgId1).Should().BeTrue();

        // Role-based check for org2 fails (scope mismatch)
        _rbacService.HasPermission(user, Permission.Deployments.Create, orgId2).Should().BeFalse();

        // API permission check is scope-independent (by design — webhook permissions don't need scope)
        _rbacService.HasPermission(user, Permission.Hooks.Redeploy, orgId2).Should().BeTrue();
    }
}
