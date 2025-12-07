using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for ConnectionConfig value object.
/// </summary>
public class ConnectionConfigTests
{
    #region DockerSocket Tests

    [Fact]
    public void DockerSocket_WithValidPath_CreatesConnectionConfig()
    {
        // Act
        var config = ConnectionConfig.DockerSocket("unix:///var/run/docker.sock");

        // Assert
        config.SocketPath.Should().Be("unix:///var/run/docker.sock");
    }

    [Fact]
    public void DockerSocket_WithWindowsPath_CreatesConnectionConfig()
    {
        // Act
        var config = ConnectionConfig.DockerSocket("npipe://./pipe/docker_engine");

        // Assert
        config.SocketPath.Should().Be("npipe://./pipe/docker_engine");
    }

    [Fact]
    public void DockerSocket_WithEmptyPath_ThrowsArgumentException()
    {
        // Act
        var act = () => ConnectionConfig.DockerSocket("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Socket path*required*");
    }

    [Fact]
    public void DockerSocket_WithNullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => ConnectionConfig.DockerSocket(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region DefaultDockerSocket Tests

    [Fact]
    public void DefaultDockerSocket_ReturnsOsAppropriateSocket()
    {
        // Act
        var config = ConnectionConfig.DefaultDockerSocket();

        // Assert
        if (OperatingSystem.IsWindows())
        {
            config.SocketPath.Should().Be("npipe://./pipe/docker_engine");
        }
        else
        {
            config.SocketPath.Should().Be("unix:///var/run/docker.sock");
        }
    }

    [Fact]
    public void DefaultDockerSocket_SocketPathIsNotEmpty()
    {
        // Act
        var config = ConnectionConfig.DefaultDockerSocket();

        // Assert
        config.SocketPath.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameSocketPath_ReturnsTrue()
    {
        // Arrange
        var c1 = ConnectionConfig.DockerSocket("unix:///var/run/docker.sock");
        var c2 = ConnectionConfig.DockerSocket("unix:///var/run/docker.sock");

        // Assert
        c1.Should().Be(c2);
    }

    [Fact]
    public void Equals_DifferentSocketPath_ReturnsFalse()
    {
        // Arrange
        var c1 = ConnectionConfig.DockerSocket("unix:///var/run/docker.sock");
        var c2 = ConnectionConfig.DockerSocket("unix:///custom/docker.sock");

        // Assert
        c1.Should().NotBe(c2);
    }

    [Fact]
    public void GetHashCode_SameSocketPath_ReturnsSameHashCode()
    {
        // Arrange
        var c1 = ConnectionConfig.DockerSocket("unix:///var/run/docker.sock");
        var c2 = ConnectionConfig.DockerSocket("unix:///var/run/docker.sock");

        // Assert
        c1.GetHashCode().Should().Be(c2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsSocketPath()
    {
        // Arrange
        var config = ConnectionConfig.DockerSocket("unix:///var/run/docker.sock");

        // Act
        var result = config.ToString();

        // Assert
        result.Should().Be("unix:///var/run/docker.sock");
    }

    #endregion
}
