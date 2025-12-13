using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.UnitTests.Authentication;

public class RbacServiceTests
{
    private readonly RbacService _rbacService;

    public RbacServiceTests()
    {
        _rbacService = new RbacService();
    }

    private ClaimsPrincipal CreateUser(params RoleAssignmentClaim[] roleAssignments)
    {
        var claims = new List<Claim>
        {
            new(RbacClaimTypes.UserId, Guid.NewGuid().ToString()),
            new(RbacClaimTypes.RoleAssignments, JsonSerializer.Serialize(roleAssignments.ToList()))
        };

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void IsSystemAdmin_WhenUserHasSystemAdminRole_ReturnsTrue()
    {
        // Arrange
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "SystemAdmin",
            Scope = "Global",
            ScopeId = null
        });

        // Act
        var result = _rbacService.IsSystemAdmin(user);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSystemAdmin_WhenUserDoesNotHaveSystemAdminRole_ReturnsFalse()
    {
        // Arrange
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "OrganizationOwner",
            Scope = "Organization",
            ScopeId = "org-123"
        });

        // Act
        var result = _rbacService.IsSystemAdmin(user);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsOrganizationOwner_WhenUserIsOwnerOfOrganization_ReturnsTrue()
    {
        // Arrange
        var orgId = "org-123";
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "OrganizationOwner",
            Scope = "Organization",
            ScopeId = orgId
        });

        // Act
        var result = _rbacService.IsOrganizationOwner(user, orgId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsOrganizationOwner_WhenUserIsOwnerOfDifferentOrganization_ReturnsFalse()
    {
        // Arrange
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "OrganizationOwner",
            Scope = "Organization",
            ScopeId = "org-123"
        });

        // Act
        var result = _rbacService.IsOrganizationOwner(user, "org-456");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_SystemAdmin_HasAllPermissions()
    {
        // Arrange
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "SystemAdmin",
            Scope = "Global",
            ScopeId = null
        });

        // Act & Assert - SystemAdmin should have all permissions
        _rbacService.HasPermission(user, Permission.Deployments.Create).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Users.Delete).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Environments.Create).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_OrganizationOwner_HasPermissionInOwnOrg()
    {
        // Arrange
        var orgId = "org-123";
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "OrganizationOwner",
            Scope = "Organization",
            ScopeId = orgId
        });

        // Act
        var result = _rbacService.HasPermission(user, Permission.Deployments.Create, orgId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_OrganizationOwner_NoPermissionInOtherOrg()
    {
        // Arrange
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "OrganizationOwner",
            Scope = "Organization",
            ScopeId = "org-123"
        });

        // Act
        var result = _rbacService.HasPermission(user, Permission.Deployments.Create, "org-456");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPermission_Operator_CanManageDeployments()
    {
        // Arrange
        var orgId = "org-123";
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "Operator",
            Scope = "Organization",
            ScopeId = orgId
        });

        // Act & Assert
        _rbacService.HasPermission(user, Permission.Deployments.Create, orgId).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Deployments.Read, orgId).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Deployments.Update, orgId).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Deployments.Delete, orgId).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_Operator_CannotManageUsers()
    {
        // Arrange
        var orgId = "org-123";
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "Operator",
            Scope = "Organization",
            ScopeId = orgId
        });

        // Act & Assert - Operators cannot manage users
        _rbacService.HasPermission(user, Permission.Users.Create, orgId).Should().BeFalse();
        _rbacService.HasPermission(user, Permission.Users.Delete, orgId).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_Viewer_CanOnlyRead()
    {
        // Arrange
        var orgId = "org-123";
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "Viewer",
            Scope = "Organization",
            ScopeId = orgId
        });

        // Act & Assert - Viewers can only read
        _rbacService.HasPermission(user, Permission.Deployments.Read, orgId).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Environments.Read, orgId).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Deployments.Create, orgId).Should().BeFalse();
        _rbacService.HasPermission(user, Permission.Deployments.Delete, orgId).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_EnvironmentScoped_OnlyAccessThatEnvironment()
    {
        // Arrange
        var envId = "env-123";
        var orgId = "org-123";
        var user = CreateUser(new RoleAssignmentClaim
        {
            Role = "Operator",
            Scope = "Environment",
            ScopeId = envId
        });

        // Act & Assert
        _rbacService.HasPermission(user, Permission.Deployments.Create, orgId, envId).Should().BeTrue();
        _rbacService.HasPermission(user, Permission.Deployments.Create, orgId, "env-456").Should().BeFalse();
    }

    [Fact]
    public void GetUserId_ReturnsUserIdFromClaims()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new(RbacClaimTypes.UserId, userId),
            new(RbacClaimTypes.RoleAssignments, "[]")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = _rbacService.GetUserId(user);

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetRoleAssignments_ReturnsEmptyListWhenNoClaim()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = _rbacService.GetRoleAssignments(user);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void HasRole_WithMultipleRoles_FindsCorrectRole()
    {
        // Arrange
        var user = CreateUser(
            new RoleAssignmentClaim { Role = "Viewer", Scope = "Organization", ScopeId = "org-1" },
            new RoleAssignmentClaim { Role = "Operator", Scope = "Environment", ScopeId = "env-1" }
        );

        // Act & Assert
        _rbacService.HasRole(user, RoleId.Viewer).Should().BeTrue();
        _rbacService.HasRole(user, RoleId.Operator).Should().BeTrue();
        _rbacService.HasRole(user, RoleId.SystemAdmin).Should().BeFalse();
        _rbacService.HasRole(user, RoleId.OrganizationOwner).Should().BeFalse();
    }
}
