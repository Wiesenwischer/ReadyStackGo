using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Catalog.Sources;

namespace ReadyStackGo.UnitTests.Domain.SharedKernel;

/// <summary>
/// Unit tests for all ID value objects.
/// </summary>
public class IdValueObjectTests
{
    #region UserId Tests

    [Fact]
    public void UserId_DefaultConstructor_GeneratesNewGuid()
    {
        // Act
        var id = new UserId();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UserId_FromGuid_SetsValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = UserId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void UserId_FromEmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => UserId.FromGuid(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void UserId_NewId_GeneratesUniqueIds()
    {
        // Act
        var id1 = UserId.NewId();
        var id2 = UserId.NewId();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void UserId_Create_GeneratesNewId()
    {
        // Act
        var id = UserId.Create();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UserId_Equality_SameGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = UserId.FromGuid(guid);
        var id2 = UserId.FromGuid(guid);

        // Assert
        id1.Should().Be(id2);
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void UserId_ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = UserId.FromGuid(guid);

        // Assert
        id.ToString().Should().Be(guid.ToString());
    }

    #endregion

    #region DeploymentId Tests

    [Fact]
    public void DeploymentId_DefaultConstructor_GeneratesNewGuid()
    {
        // Act
        var id = new DeploymentId();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void DeploymentId_FromGuid_SetsValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = DeploymentId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void DeploymentId_FromEmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => DeploymentId.FromGuid(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void DeploymentId_NewId_GeneratesUniqueIds()
    {
        // Act
        var id1 = DeploymentId.NewId();
        var id2 = DeploymentId.NewId();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void DeploymentId_Equality_SameGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = DeploymentId.FromGuid(guid);
        var id2 = DeploymentId.FromGuid(guid);

        // Assert
        id1.Should().Be(id2);
    }

    #endregion

    #region EnvironmentId Tests

    [Fact]
    public void EnvironmentId_DefaultConstructor_GeneratesNewGuid()
    {
        // Act
        var id = new EnvironmentId();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void EnvironmentId_FromGuid_SetsValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = EnvironmentId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void EnvironmentId_FromEmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => EnvironmentId.FromGuid(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void EnvironmentId_NewId_GeneratesUniqueIds()
    {
        // Act
        var id1 = EnvironmentId.NewId();
        var id2 = EnvironmentId.NewId();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void EnvironmentId_Equality_SameGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = EnvironmentId.FromGuid(guid);
        var id2 = EnvironmentId.FromGuid(guid);

        // Assert
        id1.Should().Be(id2);
    }

    #endregion

    #region OrganizationId Tests

    [Fact]
    public void OrganizationId_DefaultConstructor_GeneratesNewGuid()
    {
        // Act
        var id = new OrganizationId();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void OrganizationId_FromGuid_SetsValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = OrganizationId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void OrganizationId_FromEmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => OrganizationId.FromGuid(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void OrganizationId_NewId_GeneratesUniqueIds()
    {
        // Act
        var id1 = OrganizationId.NewId();
        var id2 = OrganizationId.NewId();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void OrganizationId_Equality_SameGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = OrganizationId.FromGuid(guid);
        var id2 = OrganizationId.FromGuid(guid);

        // Assert
        id1.Should().Be(id2);
    }

    #endregion

    #region RoleId Tests

    [Fact]
    public void RoleId_Constructor_SetsValue()
    {
        // Act
        var id = new RoleId("CustomRole");

        // Assert
        id.Value.Should().Be("CustomRole");
    }

    [Fact]
    public void RoleId_WithEmptyValue_ThrowsArgumentException()
    {
        // Act
        var act = () => new RoleId("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void RoleId_WithTooLongValue_ThrowsArgumentException()
    {
        // Arrange
        var longValue = new string('a', 51);

        // Act
        var act = () => new RoleId(longValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*50 characters*");
    }

    [Fact]
    public void RoleId_PredefinedRoles_AreAvailable()
    {
        // Assert
        RoleId.SystemAdmin.Value.Should().Be("SystemAdmin");
        RoleId.OrganizationOwner.Value.Should().Be("OrganizationOwner");
        RoleId.Operator.Value.Should().Be("Operator");
        RoleId.Viewer.Value.Should().Be("Viewer");
    }

    [Fact]
    public void RoleId_Equality_SameValue_ReturnsTrue()
    {
        // Arrange
        var id1 = new RoleId("TestRole");
        var id2 = new RoleId("TestRole");

        // Assert
        id1.Should().Be(id2);
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void RoleId_Equality_DifferentValue_ReturnsFalse()
    {
        // Arrange
        var id1 = new RoleId("Role1");
        var id2 = new RoleId("Role2");

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void RoleId_ToString_ReturnsValue()
    {
        // Arrange
        var id = new RoleId("TestRole");

        // Assert
        id.ToString().Should().Be("TestRole");
    }

    #endregion

    #region StackSourceId Tests

    [Fact]
    public void StackSourceId_Constructor_SetsValue()
    {
        // Act
        var id = new StackSourceId("my-source");

        // Assert
        id.Value.Should().Be("my-source");
    }

    [Fact]
    public void StackSourceId_WithEmptyValue_ThrowsArgumentException()
    {
        // Act
        var act = () => new StackSourceId("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void StackSourceId_WithNullValue_ThrowsArgumentException()
    {
        // Act
        var act = () => new StackSourceId(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StackSourceId_Create_SetsValue()
    {
        // Act
        var id = StackSourceId.Create("test-source");

        // Assert
        id.Value.Should().Be("test-source");
    }

    [Fact]
    public void StackSourceId_NewId_GeneratesShortUniqueId()
    {
        // Act
        var id = StackSourceId.NewId();

        // Assert
        id.Value.Should().HaveLength(8);
        id.Value.Should().MatchRegex("^[a-f0-9]{8}$");
    }

    [Fact]
    public void StackSourceId_NewId_GeneratesUniqueIds()
    {
        // Act
        var id1 = StackSourceId.NewId();
        var id2 = StackSourceId.NewId();

        // Assert
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void StackSourceId_ImplicitConversionToString()
    {
        // Arrange
        var id = new StackSourceId("my-source");

        // Act
        string value = id;

        // Assert
        value.Should().Be("my-source");
    }

    [Fact]
    public void StackSourceId_ToString_ReturnsValue()
    {
        // Arrange
        var id = new StackSourceId("my-source");

        // Assert
        id.ToString().Should().Be("my-source");
    }

    [Fact]
    public void StackSourceId_RecordEquality_SameValue_ReturnsTrue()
    {
        // Arrange
        var id1 = new StackSourceId("test");
        var id2 = new StackSourceId("test");

        // Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void StackSourceId_RecordEquality_DifferentValue_ReturnsFalse()
    {
        // Arrange
        var id1 = new StackSourceId("source1");
        var id2 = new StackSourceId("source2");

        // Assert
        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    #endregion
}
