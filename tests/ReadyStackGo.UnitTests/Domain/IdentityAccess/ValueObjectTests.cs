using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for IdentityAccess value objects.
/// </summary>
public class ValueObjectTests
{
    #region UserId Tests

    [Fact]
    public void UserId_NewId_CreatesUniqueId()
    {
        // Act
        var id1 = UserId.NewId();
        var id2 = UserId.NewId();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void UserId_Create_CreatesUniqueId()
    {
        // Act
        var id1 = UserId.Create();
        var id2 = UserId.Create();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void UserId_FromGuid_CreatesCorrectId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = UserId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void UserId_EmptyGuid_ThrowsException()
    {
        // Act
        var act = () => new UserId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UserId_Equality_WorksCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new UserId(guid);
        var id2 = new UserId(guid);

        // Assert
        id1.Should().Be(id2);
        id1.Equals(id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void UserId_ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new UserId(guid);

        // Act
        var result = id.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    #endregion

    #region OrganizationId Tests

    [Fact]
    public void OrganizationId_NewId_CreatesUniqueId()
    {
        // Act
        var id1 = OrganizationId.NewId();
        var id2 = OrganizationId.NewId();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void OrganizationId_Create_CreatesUniqueId()
    {
        // Act
        var id1 = OrganizationId.Create();
        var id2 = OrganizationId.Create();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void OrganizationId_FromGuid_CreatesCorrectId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = OrganizationId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void OrganizationId_EmptyGuid_ThrowsException()
    {
        // Act
        var act = () => new OrganizationId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OrganizationId_Equality_WorksCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new OrganizationId(guid);
        var id2 = new OrganizationId(guid);

        // Assert
        id1.Should().Be(id2);
    }

    #endregion

    #region RoleId Tests

    [Fact]
    public void RoleId_WithValidString_Creates()
    {
        // Act
        var id = new RoleId("CustomRole");

        // Assert
        id.Value.Should().Be("CustomRole");
    }

    [Fact]
    public void RoleId_EmptyString_ThrowsException()
    {
        // Act
        var act = () => new RoleId("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RoleId_TooLong_ThrowsException()
    {
        // Act
        var act = () => new RoleId(new string('x', 51));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RoleId_PredefinedRoles_HaveCorrectValues()
    {
        // Assert
        RoleId.SystemAdmin.Value.Should().Be("SystemAdmin");
        RoleId.OrganizationOwner.Value.Should().Be("OrganizationOwner");
        RoleId.Operator.Value.Should().Be("Operator");
        RoleId.Viewer.Value.Should().Be("Viewer");
    }

    [Fact]
    public void RoleId_Equality_WorksCorrectly()
    {
        // Arrange
        var id1 = new RoleId("TestRole");
        var id2 = new RoleId("TestRole");

        // Assert
        id1.Should().Be(id2);
    }

    [Fact]
    public void RoleId_ToString_ReturnsValue()
    {
        // Arrange
        var id = new RoleId("TestRole");

        // Act
        var result = id.ToString();

        // Assert
        result.Should().Be("TestRole");
    }

    #endregion

    #region EmailAddress Tests

    [Fact]
    public void EmailAddress_ValidEmail_Creates()
    {
        // Act
        var email = new EmailAddress("test@example.com");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void EmailAddress_InvalidEmail_ThrowsArgumentException()
    {
        // Act
        var act = () => new EmailAddress("not-an-email");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmailAddress_EmptyEmail_ThrowsArgumentException()
    {
        // Act
        var act = () => new EmailAddress("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmailAddress_NormalizesToLowercase()
    {
        // Act
        var email = new EmailAddress("Test@Example.COM");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void EmailAddress_Equality_WorksCorrectly()
    {
        // Arrange
        var email1 = new EmailAddress("test@example.com");
        var email2 = new EmailAddress("test@example.com");

        // Assert
        email1.Should().Be(email2);
    }

    [Fact]
    public void EmailAddress_Equality_CaseInsensitive()
    {
        // Arrange
        var email1 = new EmailAddress("TEST@example.com");
        var email2 = new EmailAddress("test@EXAMPLE.com");

        // Assert
        email1.Should().Be(email2);
    }

    [Fact]
    public void EmailAddress_ToString_ReturnsValue()
    {
        // Arrange
        var email = new EmailAddress("test@example.com");

        // Act
        var result = email.ToString();

        // Assert
        result.Should().Be("test@example.com");
    }

    #endregion

    #region Enablement Tests

    [Fact]
    public void Enablement_IndefiniteEnablement_CreatesEnabledState()
    {
        // Act
        var enablement = Enablement.IndefiniteEnablement();

        // Assert
        enablement.IsEnabled.Should().BeTrue();
        enablement.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Enablement_Disabled_CreatesDisabledState()
    {
        // Act
        var enablement = Enablement.Disabled();

        // Assert
        enablement.IsEnabled.Should().BeFalse();
        enablement.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Enablement_TimeLimited_WithValidDates_Creates()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow.AddDays(1);

        // Act
        var enablement = Enablement.TimeLimited(start, end);

        // Assert
        enablement.IsEnabled.Should().BeTrue();
        enablement.StartDate.Should().Be(start);
        enablement.EndDate.Should().Be(end);
    }

    [Fact]
    public void Enablement_TimeLimited_WithPastEndDate_ReturnsDisabled()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-10);
        var end = DateTime.UtcNow.AddDays(-5);

        // Act
        var enablement = Enablement.TimeLimited(start, end);

        // Assert
        enablement.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enablement_TimeLimited_WithFutureStartDate_ReturnsDisabled()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(5);
        var end = DateTime.UtcNow.AddDays(10);

        // Act
        var enablement = Enablement.TimeLimited(start, end);

        // Assert
        enablement.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enablement_TimeLimited_InvalidDateRange_ThrowsException()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(10);
        var end = DateTime.UtcNow.AddDays(-10);

        // Act
        var act = () => Enablement.TimeLimited(start, end);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enablement_Equality_WorksCorrectly()
    {
        // Arrange
        var e1 = Enablement.IndefiniteEnablement();
        var e2 = Enablement.IndefiniteEnablement();

        // Assert
        e1.Should().Be(e2);
    }

    #endregion

    #region RoleAssignment Tests

    [Fact]
    public void RoleAssignment_GlobalScope_CreatesCorrectly()
    {
        // Arrange
        var roleId = RoleId.Operator;

        // Act
        var assignment = RoleAssignment.Global(roleId);

        // Assert
        assignment.RoleId.Should().Be(roleId);
        assignment.ScopeType.Should().Be(ScopeType.Global);
        assignment.ScopeId.Should().BeNull();
    }

    [Fact]
    public void RoleAssignment_OrganizationScope_CreatesCorrectly()
    {
        // Arrange
        var roleId = RoleId.OrganizationOwner;
        var orgId = Guid.NewGuid().ToString();

        // Act
        var assignment = RoleAssignment.ForOrganization(roleId, orgId);

        // Assert
        assignment.RoleId.Should().Be(roleId);
        assignment.ScopeType.Should().Be(ScopeType.Organization);
        assignment.ScopeId.Should().Be(orgId);
    }

    [Fact]
    public void RoleAssignment_EnvironmentScope_CreatesCorrectly()
    {
        // Arrange
        var roleId = RoleId.Operator;
        var envId = Guid.NewGuid().ToString();

        // Act
        var assignment = RoleAssignment.ForEnvironment(roleId, envId);

        // Assert
        assignment.RoleId.Should().Be(roleId);
        assignment.ScopeType.Should().Be(ScopeType.Environment);
        assignment.ScopeId.Should().Be(envId);
    }

    [Fact]
    public void RoleAssignment_GlobalWithScopeId_ThrowsArgumentException()
    {
        // Arrange
        var roleId = RoleId.SystemAdmin;

        // Act
        var act = () => new RoleAssignment(roleId, ScopeType.Global, "some-id", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RoleAssignment_NonGlobalWithoutScopeId_ThrowsArgumentException()
    {
        // Arrange
        var roleId = RoleId.Operator;

        // Act
        var act = () => new RoleAssignment(roleId, ScopeType.Environment, null, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RoleAssignment_Equality_WorksCorrectly()
    {
        // Arrange
        var roleId = RoleId.Operator;
        var a1 = RoleAssignment.Global(roleId);
        var a2 = RoleAssignment.Global(roleId);

        // Assert - Note: AssignedAt is not part of equality
        a1.Should().Be(a2);
    }

    [Fact]
    public void RoleAssignment_ToString_ReturnsDescription()
    {
        // Arrange
        var assignment = RoleAssignment.Global(RoleId.SystemAdmin);

        // Act
        var result = assignment.ToString();

        // Assert
        result.Should().Contain("SystemAdmin");
        result.Should().Contain("Global");
    }

    #endregion

    #region Permission Tests

    [Fact]
    public void Permission_Create_CreatesCorrectly()
    {
        // Act
        var permission = new Permission("Deployments", "Create");

        // Assert
        permission.Resource.Should().Be("Deployments");
        permission.Action.Should().Be("Create");
    }

    [Fact]
    public void Permission_Equality_WorksCorrectly()
    {
        // Arrange
        var p1 = new Permission("Deployments", "Create");
        var p2 = new Permission("Deployments", "Create");

        // Assert
        p1.Should().Be(p2);
    }

    [Fact]
    public void Permission_DifferentResource_NotEqual()
    {
        // Arrange
        var p1 = new Permission("Deployments", "Create");
        var p2 = new Permission("Stacks", "Create");

        // Assert
        p1.Should().NotBe(p2);
    }

    [Fact]
    public void Permission_DifferentAction_NotEqual()
    {
        // Arrange
        var p1 = new Permission("Deployments", "Create");
        var p2 = new Permission("Deployments", "Delete");

        // Assert
        p1.Should().NotBe(p2);
    }

    [Fact]
    public void Permission_PredefinedPermissions_Exist()
    {
        // Assert - Verify predefined permissions exist
        Permission.Users.Create.Should().NotBeNull();
        Permission.Users.Read.Should().NotBeNull();
        Permission.Users.Update.Should().NotBeNull();
        Permission.Users.Delete.Should().NotBeNull();

        Permission.Environments.Create.Should().NotBeNull();
        Permission.Environments.Read.Should().NotBeNull();
        Permission.Environments.Update.Should().NotBeNull();
        Permission.Environments.Delete.Should().NotBeNull();

        Permission.Deployments.Create.Should().NotBeNull();
        Permission.Deployments.Read.Should().NotBeNull();
        Permission.Deployments.Update.Should().NotBeNull();
        Permission.Deployments.Delete.Should().NotBeNull();

        Permission.Stacks.Read.Should().NotBeNull();
        Permission.Dashboard.Read.Should().NotBeNull();
    }

    [Fact]
    public void Permission_Includes_WildcardMatchesAll()
    {
        // Arrange
        var wildcardPermission = new Permission("*", "*");
        var specificPermission = new Permission("Deployments", "Create");

        // Act
        var result = wildcardPermission.Includes(specificPermission);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Permission_Includes_ExactMatch()
    {
        // Arrange
        var p1 = new Permission("Deployments", "Create");
        var p2 = new Permission("Deployments", "Create");

        // Act
        var result = p1.Includes(p2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Permission_Includes_NoMatch()
    {
        // Arrange
        var p1 = new Permission("Deployments", "Create");
        var p2 = new Permission("Deployments", "Delete");

        // Act
        var result = p1.Includes(p2);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
