using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Roles;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for Role aggregate root.
/// Note: Roles are predefined in this system and not user-creatable.
/// </summary>
public class RoleTests
{
    #region Predefined Role Tests

    [Fact]
    public void SystemAdmin_HasCorrectProperties()
    {
        // Act
        var role = Role.SystemAdmin;

        // Assert
        role.Id.Should().Be(RoleId.SystemAdmin);
        role.Name.Should().Be("System Administrator");
        role.Description.Should().Contain("system access");
        role.AllowedScopes.Should().Be(ScopeType.Global);
    }

    [Fact]
    public void OrganizationOwner_HasCorrectProperties()
    {
        // Act
        var role = Role.OrganizationOwner;

        // Assert
        role.Id.Should().Be(RoleId.OrganizationOwner);
        role.Name.Should().Be("Organization Owner");
        role.Description.Should().Contain("organization");
        role.AllowedScopes.Should().Be(ScopeType.Organization);
    }

    [Fact]
    public void Operator_HasCorrectProperties()
    {
        // Act
        var role = Role.Operator;

        // Assert
        role.Id.Should().Be(RoleId.Operator);
        role.Name.Should().Be("Operator");
        role.Description.Should().Contain("deploy");
        role.AllowedScopes.Should().HaveFlag(ScopeType.Organization);
        role.AllowedScopes.Should().HaveFlag(ScopeType.Environment);
    }

    [Fact]
    public void Viewer_HasCorrectProperties()
    {
        // Act
        var role = Role.Viewer;

        // Assert
        role.Id.Should().Be(RoleId.Viewer);
        role.Name.Should().Be("Viewer");
        role.Description.Should().Contain("Read-only");
        role.AllowedScopes.Should().HaveFlag(ScopeType.Organization);
        role.AllowedScopes.Should().HaveFlag(ScopeType.Environment);
    }

    [Fact]
    public void GetAll_ReturnsAllPredefinedRoles()
    {
        // Act
        var roles = Role.GetAll().ToList();

        // Assert
        roles.Should().HaveCount(4);
        roles.Should().Contain(r => r.Id == RoleId.SystemAdmin);
        roles.Should().Contain(r => r.Id == RoleId.OrganizationOwner);
        roles.Should().Contain(r => r.Id == RoleId.Operator);
        roles.Should().Contain(r => r.Id == RoleId.Viewer);
    }

    [Fact]
    public void GetById_ExistingRole_ReturnsRole()
    {
        // Act
        var role = Role.GetById(RoleId.SystemAdmin);

        // Assert
        role.Should().NotBeNull();
        role!.Id.Should().Be(RoleId.SystemAdmin);
    }

    [Fact]
    public void GetById_NonExistentRole_ReturnsNull()
    {
        // Act
        var role = Role.GetById(new RoleId("NonExistent"));

        // Assert
        role.Should().BeNull();
    }

    #endregion

    #region Permission Tests

    [Fact]
    public void SystemAdmin_HasWildcardPermission()
    {
        // Arrange
        var role = Role.SystemAdmin;

        // Act & Assert
        role.HasPermission(new Permission("*", "*")).Should().BeTrue();
        role.HasPermission(Permission.Users.Create).Should().BeTrue();
        role.HasPermission(Permission.Deployments.Delete).Should().BeTrue();
        role.HasPermission(new Permission("AnyResource", "AnyAction")).Should().BeTrue();
    }

    [Fact]
    public void OrganizationOwner_HasCorrectPermissions()
    {
        // Arrange
        var role = Role.OrganizationOwner;

        // Assert - Should have these permissions
        role.HasPermission(Permission.Users.Create).Should().BeTrue();
        role.HasPermission(Permission.Users.Read).Should().BeTrue();
        role.HasPermission(Permission.Users.Update).Should().BeTrue();
        role.HasPermission(Permission.Users.Delete).Should().BeTrue();
        role.HasPermission(Permission.Environments.Create).Should().BeTrue();
        role.HasPermission(Permission.Environments.Read).Should().BeTrue();
        role.HasPermission(Permission.Environments.Update).Should().BeTrue();
        role.HasPermission(Permission.Environments.Delete).Should().BeTrue();
        role.HasPermission(Permission.Deployments.Create).Should().BeTrue();
        role.HasPermission(Permission.Deployments.Read).Should().BeTrue();
        role.HasPermission(Permission.Deployments.Update).Should().BeTrue();
        role.HasPermission(Permission.Deployments.Delete).Should().BeTrue();
        role.HasPermission(Permission.StackSources.Create).Should().BeTrue();
        role.HasPermission(Permission.StackSources.Read).Should().BeTrue();
        role.HasPermission(Permission.StackSources.Update).Should().BeTrue();
        role.HasPermission(Permission.StackSources.Delete).Should().BeTrue();
        role.HasPermission(Permission.Stacks.Read).Should().BeTrue();
        role.HasPermission(Permission.Dashboard.Read).Should().BeTrue();
    }

    [Fact]
    public void Operator_HasCorrectPermissions()
    {
        // Arrange
        var role = Role.Operator;

        // Assert - Should have these permissions
        role.HasPermission(Permission.Deployments.Create).Should().BeTrue();
        role.HasPermission(Permission.Deployments.Read).Should().BeTrue();
        role.HasPermission(Permission.Deployments.Update).Should().BeTrue();
        role.HasPermission(Permission.Deployments.Delete).Should().BeTrue();
        role.HasPermission(Permission.Environments.Read).Should().BeTrue();
        role.HasPermission(Permission.StackSources.Read).Should().BeTrue();
        role.HasPermission(Permission.Stacks.Read).Should().BeTrue();
        role.HasPermission(Permission.Dashboard.Read).Should().BeTrue();

        // Should NOT have these permissions
        role.HasPermission(Permission.Users.Create).Should().BeFalse();
        role.HasPermission(Permission.Users.Delete).Should().BeFalse();
        role.HasPermission(Permission.Environments.Create).Should().BeFalse();
        role.HasPermission(Permission.Environments.Delete).Should().BeFalse();
    }

    [Fact]
    public void Viewer_HasReadOnlyPermissions()
    {
        // Arrange
        var role = Role.Viewer;

        // Assert - Should have these permissions
        role.HasPermission(Permission.Deployments.Read).Should().BeTrue();
        role.HasPermission(Permission.Environments.Read).Should().BeTrue();
        role.HasPermission(Permission.StackSources.Read).Should().BeTrue();
        role.HasPermission(Permission.Stacks.Read).Should().BeTrue();
        role.HasPermission(Permission.Dashboard.Read).Should().BeTrue();

        // Should NOT have these permissions
        role.HasPermission(Permission.Users.Create).Should().BeFalse();
        role.HasPermission(Permission.Users.Read).Should().BeFalse();
        role.HasPermission(Permission.Deployments.Create).Should().BeFalse();
        role.HasPermission(Permission.Deployments.Delete).Should().BeFalse();
        role.HasPermission(Permission.Environments.Create).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var role = Role.Operator;

        // Act
        var result = role.HasPermission(Permission.Deployments.Create);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_NoMatch_ReturnsFalse()
    {
        // Arrange
        var role = Role.Viewer;

        // Act
        var result = role.HasPermission(Permission.Users.Create);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Scope Assignment Tests

    [Fact]
    public void CanBeAssignedToScope_SystemAdmin_OnlyGlobal()
    {
        // Arrange
        var role = Role.SystemAdmin;

        // Assert
        role.CanBeAssignedToScope(ScopeType.Global).Should().BeTrue();
        role.CanBeAssignedToScope(ScopeType.Organization).Should().BeFalse();
        role.CanBeAssignedToScope(ScopeType.Environment).Should().BeFalse();
    }

    [Fact]
    public void CanBeAssignedToScope_OrganizationOwner_OnlyOrganization()
    {
        // Arrange
        var role = Role.OrganizationOwner;

        // Assert
        role.CanBeAssignedToScope(ScopeType.Global).Should().BeFalse();
        role.CanBeAssignedToScope(ScopeType.Organization).Should().BeTrue();
        role.CanBeAssignedToScope(ScopeType.Environment).Should().BeFalse();
    }

    [Fact]
    public void CanBeAssignedToScope_Operator_OrganizationAndEnvironment()
    {
        // Arrange
        var role = Role.Operator;

        // Assert
        role.CanBeAssignedToScope(ScopeType.Global).Should().BeFalse();
        role.CanBeAssignedToScope(ScopeType.Organization).Should().BeTrue();
        role.CanBeAssignedToScope(ScopeType.Environment).Should().BeTrue();
    }

    [Fact]
    public void CanBeAssignedToScope_Viewer_OrganizationAndEnvironment()
    {
        // Arrange
        var role = Role.Viewer;

        // Assert
        role.CanBeAssignedToScope(ScopeType.Global).Should().BeFalse();
        role.CanBeAssignedToScope(ScopeType.Organization).Should().BeTrue();
        role.CanBeAssignedToScope(ScopeType.Environment).Should().BeTrue();
    }

    #endregion

    #region Permissions Collection Tests

    [Fact]
    public void Permissions_IsReadOnly()
    {
        // Arrange
        var role = Role.Operator;

        // Act
        var permissions = role.Permissions;

        // Assert
        permissions.Should().BeAssignableTo<IReadOnlyCollection<Permission>>();
    }

    [Fact]
    public void SystemAdmin_HasSingleWildcardPermission()
    {
        // Arrange
        var role = Role.SystemAdmin;

        // Assert
        role.Permissions.Should().ContainSingle();
        role.Permissions.First().Resource.Should().Be("*");
        role.Permissions.First().Action.Should().Be("*");
    }

    [Fact]
    public void Operator_HasMultiplePermissions()
    {
        // Arrange
        var role = Role.Operator;

        // Assert
        role.Permissions.Should().HaveCountGreaterThan(1);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var role = Role.SystemAdmin;

        // Act
        var result = role.ToString();

        // Assert
        result.Should().Contain("SystemAdmin");
        result.Should().Contain("System Administrator");
    }

    #endregion
}
