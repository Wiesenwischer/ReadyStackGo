using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;
using EnvironmentId = ReadyStackGo.Domain.Deployment.Environments.EnvironmentId;
using EnvironmentType = ReadyStackGo.Domain.Deployment.Environments.EnvironmentType;
using EnvironmentCreated = ReadyStackGo.Domain.Deployment.Environments.EnvironmentCreated;
using ConnectionConfig = ReadyStackGo.Domain.Deployment.Environments.ConnectionConfig;

namespace ReadyStackGo.UnitTests.Domain.EnvironmentTests;

/// <summary>
/// Unit tests for Environment aggregate root.
/// </summary>
public class EnvironmentTests
{
    #region Creation Tests

    [Fact]
    public void CreateDockerSocket_WithValidData_CreatesEnvironment()
    {
        // Arrange
        var envId = EnvironmentId.NewId();
        var orgId = OrganizationId.NewId();
        var socketPath = "/var/run/docker.sock";

        // Act
        var env = Environment.CreateDockerSocket(
            envId,
            orgId,
            "Production",
            "Production Docker environment",
            socketPath);

        // Assert
        env.Id.Should().Be(envId);
        env.OrganizationId.Should().Be(orgId);
        env.Name.Should().Be("Production");
        env.Description.Should().Be("Production Docker environment");
        env.Type.Should().Be(EnvironmentType.DockerSocket);
        env.ConnectionConfig.SocketPath.Should().Be(socketPath);
        env.IsDefault.Should().BeFalse();
        env.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        env.DomainEvents.Should().ContainSingle(e => e is EnvironmentCreated);
    }

    [Fact]
    public void CreateDefault_CreatesEnvironmentWithDefaultSocket()
    {
        // Arrange
        var envId = EnvironmentId.NewId();
        var orgId = OrganizationId.NewId();

        // Act
        var env = Environment.CreateDefault(envId, orgId, "Default");

        // Assert
        env.Id.Should().Be(envId);
        env.OrganizationId.Should().Be(orgId);
        env.Name.Should().Be("Default");
        env.Type.Should().Be(EnvironmentType.DockerSocket);
        env.ConnectionConfig.SocketPath.Should().NotBeNullOrEmpty();
        env.DomainEvents.Should().ContainSingle(e => e is EnvironmentCreated);
    }

    [Fact]
    public void CreateDockerSocket_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => Environment.CreateDockerSocket(
            EnvironmentId.NewId(),
            OrganizationId.NewId(),
            "",
            null,
            "/var/run/docker.sock");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDockerSocket_WithNullEnvironmentId_ThrowsArgumentException()
    {
        // Act
        var act = () => Environment.CreateDockerSocket(
            null!,
            OrganizationId.NewId(),
            "Test",
            null,
            "/var/run/docker.sock");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDockerSocket_WithNullOrganizationId_ThrowsArgumentException()
    {
        // Act
        var act = () => Environment.CreateDockerSocket(
            EnvironmentId.NewId(),
            null!,
            "Test",
            null,
            "/var/run/docker.sock");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDockerSocket_WithNameTooLong_ThrowsArgumentException()
    {
        // Act
        var act = () => Environment.CreateDockerSocket(
            EnvironmentId.NewId(),
            OrganizationId.NewId(),
            new string('x', 101),
            null,
            "/var/run/docker.sock");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDockerSocket_RaisesEnvironmentCreatedEvent()
    {
        // Arrange
        var envId = EnvironmentId.NewId();

        // Act
        var env = Environment.CreateDockerSocket(
            envId,
            OrganizationId.NewId(),
            "Production",
            null,
            "/var/run/docker.sock");

        // Assert
        var domainEvent = env.DomainEvents.OfType<EnvironmentCreated>().Single();
        domainEvent.EnvironmentId.Should().Be(envId);
        domainEvent.Name.Should().Be("Production");
    }

    #endregion

    #region UpdateName Tests

    [Fact]
    public void UpdateName_WithValidName_ChangesName()
    {
        // Arrange
        var env = CreateTestEnvironment();

        // Act
        env.UpdateName("Staging");

        // Assert
        env.Name.Should().Be("Staging");
        env.UpdatedAt.Should().NotBeNull();
        env.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateName_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var env = CreateTestEnvironment();

        // Act
        var act = () => env.UpdateName("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateName_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var env = CreateTestEnvironment();

        // Act
        var act = () => env.UpdateName(new string('x', 101));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Connection Config Tests

    [Fact]
    public void UpdateConnectionConfig_ValidConfig_UpdatesConfig()
    {
        // Arrange
        var env = CreateTestEnvironment();
        var newConfig = ConnectionConfig.DockerSocket("/new/docker.sock");

        // Act
        env.UpdateConnectionConfig(newConfig);

        // Assert
        env.ConnectionConfig.SocketPath.Should().Be("/new/docker.sock");
        env.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateConnectionConfig_NullConfig_ThrowsArgumentException()
    {
        // Arrange
        var env = CreateTestEnvironment();

        // Act
        var act = () => env.UpdateConnectionConfig(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Default Environment Tests

    [Fact]
    public void SetAsDefault_SetsIsDefaultTrue()
    {
        // Arrange
        var env = CreateTestEnvironment();
        env.IsDefault.Should().BeFalse();

        // Act
        env.SetAsDefault();

        // Assert
        env.IsDefault.Should().BeTrue();
        env.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UnsetAsDefault_SetsIsDefaultFalse()
    {
        // Arrange
        var env = CreateTestEnvironment();
        env.SetAsDefault();

        // Act
        env.UnsetAsDefault();

        // Assert
        env.IsDefault.Should().BeFalse();
    }

    #endregion

    #region EnvironmentId Tests

    [Fact]
    public void EnvironmentId_NewId_CreatesUniqueId()
    {
        // Act
        var id1 = EnvironmentId.NewId();
        var id2 = EnvironmentId.NewId();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void EnvironmentId_Create_CreatesUniqueId()
    {
        // Act
        var id1 = EnvironmentId.Create();
        var id2 = EnvironmentId.Create();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void EnvironmentId_FromGuid_CreatesCorrectId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = EnvironmentId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void EnvironmentId_EmptyGuid_ThrowsException()
    {
        // Act
        var act = () => new EnvironmentId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnvironmentId_Equality_WorksCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new EnvironmentId(guid);
        var id2 = new EnvironmentId(guid);

        // Assert
        id1.Should().Be(id2);
        id1.Equals(id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void EnvironmentId_ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new EnvironmentId(guid);

        // Act
        var result = id.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    #endregion

    #region ConnectionConfig Tests

    [Fact]
    public void ConnectionConfig_DockerSocket_CreatesWithPath()
    {
        // Act
        var config = ConnectionConfig.DockerSocket("/var/run/docker.sock");

        // Assert
        config.SocketPath.Should().Be("/var/run/docker.sock");
    }

    [Fact]
    public void ConnectionConfig_DockerSocket_EmptyPath_ThrowsException()
    {
        // Act
        var act = () => ConnectionConfig.DockerSocket("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConnectionConfig_DefaultDockerSocket_CreatesWithOsSpecificPath()
    {
        // Act
        var config = ConnectionConfig.DefaultDockerSocket();

        // Assert
        config.SocketPath.Should().NotBeNullOrEmpty();
        if (OperatingSystem.IsWindows())
        {
            config.SocketPath.Should().Contain("npipe://");
        }
        else
        {
            config.SocketPath.Should().Contain("unix://");
        }
    }

    [Fact]
    public void ConnectionConfig_Equality_WorksCorrectly()
    {
        // Arrange
        var config1 = ConnectionConfig.DockerSocket("/var/run/docker.sock");
        var config2 = ConnectionConfig.DockerSocket("/var/run/docker.sock");

        // Assert
        config1.Should().Be(config2);
    }

    [Fact]
    public void ConnectionConfig_DifferentPath_NotEqual()
    {
        // Arrange
        var config1 = ConnectionConfig.DockerSocket("/var/run/docker.sock");
        var config2 = ConnectionConfig.DockerSocket("/other/docker.sock");

        // Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void ConnectionConfig_ToString_ReturnsPath()
    {
        // Arrange
        var config = ConnectionConfig.DockerSocket("/var/run/docker.sock");

        // Act
        var result = config.ToString();

        // Assert
        result.Should().Be("/var/run/docker.sock");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var env = CreateTestEnvironment();

        // Act
        var result = env.ToString();

        // Assert
        result.Should().Contain("Development");
        result.Should().Contain("DockerSocket");
    }

    #endregion

    #region Helper Methods

    private static Environment CreateTestEnvironment()
    {
        return Environment.CreateDockerSocket(
            EnvironmentId.NewId(),
            OrganizationId.NewId(),
            "Development",
            "Development Docker environment",
            "/var/run/docker.sock");
    }

    #endregion
}
