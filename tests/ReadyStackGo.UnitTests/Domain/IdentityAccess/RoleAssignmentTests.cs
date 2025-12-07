using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for RoleAssignment value object.
/// </summary>
public class RoleAssignmentTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithGlobalScope_CreatesAssignment()
    {
        // Arrange
        var assignedAt = DateTime.UtcNow;

        // Act
        var assignment = new RoleAssignment(RoleId.SystemAdmin, ScopeType.Global, null, assignedAt);

        // Assert
        assignment.RoleId.Should().Be(RoleId.SystemAdmin);
        assignment.ScopeType.Should().Be(ScopeType.Global);
        assignment.ScopeId.Should().BeNull();
        assignment.AssignedAt.Should().Be(assignedAt);
    }

    [Fact]
    public void Constructor_WithOrganizationScope_CreatesAssignment()
    {
        // Arrange
        var assignedAt = DateTime.UtcNow;
        var orgId = "org-123";

        // Act
        var assignment = new RoleAssignment(RoleId.OrganizationOwner, ScopeType.Organization, orgId, assignedAt);

        // Assert
        assignment.RoleId.Should().Be(RoleId.OrganizationOwner);
        assignment.ScopeType.Should().Be(ScopeType.Organization);
        assignment.ScopeId.Should().Be(orgId);
    }

    [Fact]
    public void Constructor_WithEnvironmentScope_CreatesAssignment()
    {
        // Arrange
        var assignedAt = DateTime.UtcNow;
        var envId = "env-456";

        // Act
        var assignment = new RoleAssignment(RoleId.Operator, ScopeType.Environment, envId, assignedAt);

        // Assert
        assignment.RoleId.Should().Be(RoleId.Operator);
        assignment.ScopeType.Should().Be(ScopeType.Environment);
        assignment.ScopeId.Should().Be(envId);
    }

    [Fact]
    public void Constructor_WithNullRoleId_ThrowsArgumentException()
    {
        // Act
        var act = () => new RoleAssignment(null!, ScopeType.Global, null, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*RoleId*required*");
    }

    [Fact]
    public void Constructor_GlobalScopeWithScopeId_ThrowsArgumentException()
    {
        // Act
        var act = () => new RoleAssignment(RoleId.SystemAdmin, ScopeType.Global, "some-id", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Global scope should not have a ScopeId*");
    }

    [Fact]
    public void Constructor_OrganizationScopeWithoutScopeId_ThrowsArgumentException()
    {
        // Act
        var act = () => new RoleAssignment(RoleId.OrganizationOwner, ScopeType.Organization, null, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Non-global scope requires a ScopeId*");
    }

    [Fact]
    public void Constructor_OrganizationScopeWithEmptyScopeId_ThrowsArgumentException()
    {
        // Act
        var act = () => new RoleAssignment(RoleId.OrganizationOwner, ScopeType.Organization, "", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Non-global scope requires a ScopeId*");
    }

    [Fact]
    public void Constructor_EnvironmentScopeWithoutScopeId_ThrowsArgumentException()
    {
        // Act
        var act = () => new RoleAssignment(RoleId.Operator, ScopeType.Environment, null, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Non-global scope requires a ScopeId*");
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void Global_CreatesGlobalScopedAssignment()
    {
        // Act
        var assignment = RoleAssignment.Global(RoleId.SystemAdmin);

        // Assert
        assignment.RoleId.Should().Be(RoleId.SystemAdmin);
        assignment.ScopeType.Should().Be(ScopeType.Global);
        assignment.ScopeId.Should().BeNull();
        assignment.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ForOrganization_CreatesOrgScopedAssignment()
    {
        // Arrange
        var orgId = "org-123";

        // Act
        var assignment = RoleAssignment.ForOrganization(RoleId.OrganizationOwner, orgId);

        // Assert
        assignment.RoleId.Should().Be(RoleId.OrganizationOwner);
        assignment.ScopeType.Should().Be(ScopeType.Organization);
        assignment.ScopeId.Should().Be(orgId);
        assignment.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ForEnvironment_CreatesEnvScopedAssignment()
    {
        // Arrange
        var envId = "env-456";

        // Act
        var assignment = RoleAssignment.ForEnvironment(RoleId.Operator, envId);

        // Assert
        assignment.RoleId.Should().Be(RoleId.Operator);
        assignment.ScopeType.Should().Be(ScopeType.Environment);
        assignment.ScopeId.Should().Be(envId);
        assignment.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameRoleAndScope_ReturnsTrue()
    {
        // Arrange
        var assignedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var a1 = new RoleAssignment(RoleId.SystemAdmin, ScopeType.Global, null, assignedAt);
        var a2 = new RoleAssignment(RoleId.SystemAdmin, ScopeType.Global, null, assignedAt.AddDays(1)); // Different time

        // Assert - equality is based on RoleId, ScopeType, ScopeId (not AssignedAt)
        a1.Should().Be(a2);
    }

    [Fact]
    public void Equals_DifferentRole_ReturnsFalse()
    {
        // Arrange
        var a1 = RoleAssignment.Global(RoleId.SystemAdmin);
        var a2 = RoleAssignment.Global(RoleId.OrganizationOwner);

        // Assert
        a1.Should().NotBe(a2);
    }

    [Fact]
    public void Equals_DifferentScopeType_ReturnsFalse()
    {
        // Arrange
        var a1 = RoleAssignment.Global(RoleId.OrganizationOwner);
        var a2 = RoleAssignment.ForOrganization(RoleId.OrganizationOwner, "org-123");

        // Assert
        a1.Should().NotBe(a2);
    }

    [Fact]
    public void Equals_DifferentScopeId_ReturnsFalse()
    {
        // Arrange
        var a1 = RoleAssignment.ForOrganization(RoleId.OrganizationOwner, "org-123");
        var a2 = RoleAssignment.ForOrganization(RoleId.OrganizationOwner, "org-456");

        // Assert
        a1.Should().NotBe(a2);
    }

    [Fact]
    public void GetHashCode_SameRoleAndScope_ReturnsSameHashCode()
    {
        // Arrange
        var a1 = RoleAssignment.ForOrganization(RoleId.OrganizationOwner, "org-123");
        var a2 = RoleAssignment.ForOrganization(RoleId.OrganizationOwner, "org-123");

        // Assert
        a1.GetHashCode().Should().Be(a2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_GlobalScope_ReturnsDescriptiveString()
    {
        // Arrange
        var assignment = RoleAssignment.Global(RoleId.SystemAdmin);

        // Act
        var result = assignment.ToString();

        // Assert
        result.Should().Contain("RoleAssignment");
        result.Should().Contain("SystemAdmin");
        result.Should().Contain("Global");
    }

    [Fact]
    public void ToString_OrgScope_ContainsScopeId()
    {
        // Arrange
        var assignment = RoleAssignment.ForOrganization(RoleId.OrganizationOwner, "org-123");

        // Act
        var result = assignment.ToString();

        // Assert
        result.Should().Contain("org-123");
        result.Should().Contain("Organization");
    }

    #endregion
}
