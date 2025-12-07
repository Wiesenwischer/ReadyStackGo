using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Roles;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for Permission value object.
/// </summary>
public class PermissionTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidResourceAndAction_CreatesPermission()
    {
        // Act
        var permission = new Permission("Users", "Create");

        // Assert
        permission.Resource.Should().Be("Users");
        permission.Action.Should().Be("Create");
    }

    [Fact]
    public void Constructor_WithEmptyResource_ThrowsArgumentException()
    {
        // Act
        var act = () => new Permission("", "Create");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*resource*required*");
    }

    [Fact]
    public void Constructor_WithNullResource_ThrowsArgumentException()
    {
        // Act
        var act = () => new Permission(null!, "Create");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyAction_ThrowsArgumentException()
    {
        // Act
        var act = () => new Permission("Users", "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*action*required*");
    }

    [Fact]
    public void Constructor_WithNullAction_ThrowsArgumentException()
    {
        // Act
        var act = () => new Permission("Users", null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_WithValidString_ReturnsPermission()
    {
        // Act
        var permission = Permission.Parse("Users.Create");

        // Assert
        permission.Resource.Should().Be("Users");
        permission.Action.Should().Be("Create");
    }

    [Theory]
    [InlineData("Deployments.Read")]
    [InlineData("Environments.Delete")]
    [InlineData("StackSources.Update")]
    public void Parse_WithVariousValidFormats_Succeeds(string permissionString)
    {
        // Act
        var permission = Permission.Parse(permissionString);

        // Assert
        permission.ToString().Should().Be(permissionString);
    }

    [Fact]
    public void Parse_WithEmptyString_ThrowsArgumentException()
    {
        // Act
        var act = () => Permission.Parse("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Permission string is required*");
    }

    [Fact]
    public void Parse_WithNullString_ThrowsArgumentException()
    {
        // Act
        var act = () => Permission.Parse(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Invalid")]           // No dot
    [InlineData("Too.Many.Parts")]    // Too many dots
    [InlineData(".Action")]           // Empty resource
    [InlineData("Resource.")]         // Empty action
    public void Parse_WithInvalidFormat_ThrowsArgumentException(string invalidString)
    {
        // Act
        var act = () => Permission.Parse(invalidString);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Includes Tests (Wildcard Support)

    [Fact]
    public void Includes_WildcardResource_IncludesAnyPermission()
    {
        // Arrange
        var wildcardPermission = new Permission("*", "Create");
        var specificPermission = new Permission("Users", "Create");

        // Act
        var result = wildcardPermission.Includes(specificPermission);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Includes_WildcardAction_IncludesAnyActionOnResource()
    {
        // Arrange
        var wildcardPermission = new Permission("Users", "*");
        var specificPermission = new Permission("Users", "Delete");

        // Act
        var result = wildcardPermission.Includes(specificPermission);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Includes_FullWildcard_IncludesEverything()
    {
        // Arrange
        var fullWildcard = new Permission("*", "*");
        var anyPermission = new Permission("Deployments", "Create");

        // Act
        var result = fullWildcard.Includes(anyPermission);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Includes_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var p1 = new Permission("Users", "Create");
        var p2 = new Permission("Users", "Create");

        // Act
        var result = p1.Includes(p2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Includes_DifferentResource_ReturnsFalse()
    {
        // Arrange
        var p1 = new Permission("Users", "Create");
        var p2 = new Permission("Deployments", "Create");

        // Act
        var result = p1.Includes(p2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Includes_DifferentAction_ReturnsFalse()
    {
        // Arrange
        var p1 = new Permission("Users", "Create");
        var p2 = new Permission("Users", "Delete");

        // Act
        var result = p1.Includes(p2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Includes_WildcardActionDifferentResource_ReturnsFalse()
    {
        // Arrange
        var p1 = new Permission("Users", "*");
        var p2 = new Permission("Deployments", "Create");

        // Act
        var result = p1.Includes(p2);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Pre-defined Permissions Tests

    [Fact]
    public void Users_Create_ReturnsCorrectPermission()
    {
        // Act
        var permission = Permission.Users.Create;

        // Assert
        permission.Resource.Should().Be("Users");
        permission.Action.Should().Be("Create");
    }

    [Fact]
    public void Users_Read_ReturnsCorrectPermission()
    {
        // Act
        var permission = Permission.Users.Read;

        // Assert
        permission.Resource.Should().Be("Users");
        permission.Action.Should().Be("Read");
    }

    [Fact]
    public void Deployments_Create_ReturnsCorrectPermission()
    {
        // Act
        var permission = Permission.Deployments.Create;

        // Assert
        permission.Resource.Should().Be("Deployments");
        permission.Action.Should().Be("Create");
    }

    [Fact]
    public void Environments_Delete_ReturnsCorrectPermission()
    {
        // Act
        var permission = Permission.Environments.Delete;

        // Assert
        permission.Resource.Should().Be("Environments");
        permission.Action.Should().Be("Delete");
    }

    [Fact]
    public void StackSources_Update_ReturnsCorrectPermission()
    {
        // Act
        var permission = Permission.StackSources.Update;

        // Assert
        permission.Resource.Should().Be("StackSources");
        permission.Action.Should().Be("Update");
    }

    [Fact]
    public void Stacks_Read_ReturnsCorrectPermission()
    {
        // Act
        var permission = Permission.Stacks.Read;

        // Assert
        permission.Resource.Should().Be("Stacks");
        permission.Action.Should().Be("Read");
    }

    [Fact]
    public void Dashboard_Read_ReturnsCorrectPermission()
    {
        // Act
        var permission = Permission.Dashboard.Read;

        // Assert
        permission.Resource.Should().Be("Dashboard");
        permission.Action.Should().Be("Read");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameResourceAndAction_ReturnsTrue()
    {
        // Arrange
        var p1 = new Permission("Users", "Create");
        var p2 = new Permission("Users", "Create");

        // Assert
        p1.Should().Be(p2);
    }

    [Fact]
    public void Equals_DifferentPermissions_ReturnsFalse()
    {
        // Arrange
        var p1 = new Permission("Users", "Create");
        var p2 = new Permission("Users", "Delete");

        // Assert
        p1.Should().NotBe(p2);
    }

    [Fact]
    public void GetHashCode_SamePermission_ReturnsSameHashCode()
    {
        // Arrange
        var p1 = new Permission("Users", "Create");
        var p2 = new Permission("Users", "Create");

        // Assert
        p1.GetHashCode().Should().Be(p2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsResourceDotAction()
    {
        // Arrange
        var permission = new Permission("Users", "Create");

        // Act
        var result = permission.ToString();

        // Assert
        result.Should().Be("Users.Create");
    }

    #endregion
}
