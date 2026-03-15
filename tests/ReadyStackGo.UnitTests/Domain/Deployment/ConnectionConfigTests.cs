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
        var config = DockerSocketConfig.Create("unix:///var/run/docker.sock");

        // Assert
        config.GetDockerHost().Should().Be("unix:///var/run/docker.sock");
    }

    [Fact]
    public void DockerSocket_WithWindowsPath_CreatesConnectionConfig()
    {
        // Act
        var config = DockerSocketConfig.Create("npipe://./pipe/docker_engine");

        // Assert
        config.GetDockerHost().Should().Be("npipe://./pipe/docker_engine");
    }

    [Fact]
    public void DockerSocket_WithEmptyPath_ThrowsArgumentException()
    {
        // Act
        var act = () => DockerSocketConfig.Create("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Socket path*required*");
    }

    [Fact]
    public void DockerSocket_WithNullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => DockerSocketConfig.Create(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region DefaultDockerSocket Tests

    [Fact]
    public void DefaultDockerSocket_ReturnsOsAppropriateSocket()
    {
        // Act
        var config = DockerSocketConfig.DefaultForOs();

        // Assert
        if (OperatingSystem.IsWindows())
        {
            config.GetDockerHost().Should().Be("npipe://./pipe/docker_engine");
        }
        else
        {
            config.GetDockerHost().Should().Be("unix:///var/run/docker.sock");
        }
    }

    [Fact]
    public void DefaultDockerSocket_SocketPathIsNotEmpty()
    {
        // Act
        var config = DockerSocketConfig.DefaultForOs();

        // Assert
        config.GetDockerHost().Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameSocketPath_ReturnsTrue()
    {
        // Arrange
        var c1 = DockerSocketConfig.Create("unix:///var/run/docker.sock");
        var c2 = DockerSocketConfig.Create("unix:///var/run/docker.sock");

        // Assert
        c1.Should().Be(c2);
    }

    [Fact]
    public void Equals_DifferentSocketPath_ReturnsFalse()
    {
        // Arrange
        var c1 = DockerSocketConfig.Create("unix:///var/run/docker.sock");
        var c2 = DockerSocketConfig.Create("unix:///custom/docker.sock");

        // Assert
        c1.Should().NotBe(c2);
    }

    [Fact]
    public void GetHashCode_SameSocketPath_ReturnsSameHashCode()
    {
        // Arrange
        var c1 = DockerSocketConfig.Create("unix:///var/run/docker.sock");
        var c2 = DockerSocketConfig.Create("unix:///var/run/docker.sock");

        // Assert
        c1.GetHashCode().Should().Be(c2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsSocketPath()
    {
        // Arrange
        var config = DockerSocketConfig.Create("unix:///var/run/docker.sock");

        // Act
        var result = config.ToString();

        // Assert
        result.Should().Be("unix:///var/run/docker.sock");
    }

    #endregion

    #region SshTunnelConfig Tests

    [Fact]
    public void SshTunnelConfig_Create_WithValidData_ReturnsConfig()
    {
        // Act
        var config = SshTunnelConfig.Create("192.168.1.100", 22, "root", SshAuthMethod.PrivateKey);

        // Assert
        config.Host.Should().Be("192.168.1.100");
        config.Port.Should().Be(22);
        config.Username.Should().Be("root");
        config.AuthMethod.Should().Be(SshAuthMethod.PrivateKey);
        config.RemoteSocketPath.Should().Be("/var/run/docker.sock");
        config.ConfigType.Should().Be("SshTunnel");
    }

    [Fact]
    public void SshTunnelConfig_Create_WithCustomRemoteSocketPath_UsesIt()
    {
        // Act
        var config = SshTunnelConfig.Create("host", 2222, "user", SshAuthMethod.Password, "/custom/docker.sock");

        // Assert
        config.RemoteSocketPath.Should().Be("/custom/docker.sock");
        config.Port.Should().Be(2222);
        config.AuthMethod.Should().Be(SshAuthMethod.Password);
    }

    [Fact]
    public void SshTunnelConfig_GetDockerHost_ReturnsSshUri()
    {
        // Act
        var config = SshTunnelConfig.Create("myserver.com", 22, "deploy", SshAuthMethod.PrivateKey);

        // Assert
        config.GetDockerHost().Should().Be("ssh://deploy@myserver.com:22");
    }

    [Fact]
    public void SshTunnelConfig_Create_WithEmptyHost_ThrowsArgumentException()
    {
        // Act
        var act = () => SshTunnelConfig.Create("", 22, "root", SshAuthMethod.PrivateKey);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*host*required*");
    }

    [Fact]
    public void SshTunnelConfig_Create_WithEmptyUsername_ThrowsArgumentException()
    {
        // Act
        var act = () => SshTunnelConfig.Create("host", 22, "", SshAuthMethod.PrivateKey);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*username*required*");
    }

    [Fact]
    public void SshTunnelConfig_Create_WithEmptyRemoteSocketPath_ThrowsArgumentException()
    {
        // Act
        var act = () => SshTunnelConfig.Create("host", 22, "root", SshAuthMethod.PrivateKey, "");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*socket*required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void SshTunnelConfig_Create_WithInvalidPort_ThrowsArgumentException(int port)
    {
        // Act
        var act = () => SshTunnelConfig.Create("host", port, "root", SshAuthMethod.PrivateKey);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*port*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(22)]
    [InlineData(2222)]
    [InlineData(65535)]
    public void SshTunnelConfig_Create_WithValidPort_Succeeds(int port)
    {
        // Act
        var config = SshTunnelConfig.Create("host", port, "root", SshAuthMethod.PrivateKey);

        // Assert
        config.Port.Should().Be(port);
    }

    [Fact]
    public void SshTunnelConfig_Equality_SameValues_AreEqual()
    {
        // Arrange
        var c1 = SshTunnelConfig.Create("host", 22, "root", SshAuthMethod.PrivateKey);
        var c2 = SshTunnelConfig.Create("host", 22, "root", SshAuthMethod.PrivateKey);

        // Assert
        c1.Should().Be(c2);
        c1.GetHashCode().Should().Be(c2.GetHashCode());
    }

    [Fact]
    public void SshTunnelConfig_Equality_DifferentHost_NotEqual()
    {
        // Arrange
        var c1 = SshTunnelConfig.Create("host1", 22, "root", SshAuthMethod.PrivateKey);
        var c2 = SshTunnelConfig.Create("host2", 22, "root", SshAuthMethod.PrivateKey);

        // Assert
        c1.Should().NotBe(c2);
    }

    [Fact]
    public void SshTunnelConfig_Equality_DifferentAuthMethod_NotEqual()
    {
        // Arrange
        var c1 = SshTunnelConfig.Create("host", 22, "root", SshAuthMethod.PrivateKey);
        var c2 = SshTunnelConfig.Create("host", 22, "root", SshAuthMethod.Password);

        // Assert
        c1.Should().NotBe(c2);
    }

    [Fact]
    public void SshTunnelConfig_NotEqual_ToDockerSocketConfig()
    {
        // Arrange
        ConnectionConfig ssh = SshTunnelConfig.Create("host", 22, "root", SshAuthMethod.PrivateKey);
        ConnectionConfig socket = DockerSocketConfig.Create("/var/run/docker.sock");

        // Assert
        ssh.Should().NotBe(socket);
    }

    #endregion

    #region SshCredential Tests

    [Fact]
    public void SshCredential_Create_WithValidData_ReturnsCredential()
    {
        // Act
        var credential = SshCredential.Create("encrypted-data", SshAuthMethod.PrivateKey);

        // Assert
        credential.EncryptedSecret.Should().Be("encrypted-data");
        credential.AuthMethod.Should().Be(SshAuthMethod.PrivateKey);
    }

    [Fact]
    public void SshCredential_Create_WithPasswordAuth_ReturnsCredential()
    {
        // Act
        var credential = SshCredential.Create("encrypted-password", SshAuthMethod.Password);

        // Assert
        credential.AuthMethod.Should().Be(SshAuthMethod.Password);
    }

    [Fact]
    public void SshCredential_Create_WithEmptySecret_ThrowsArgumentException()
    {
        // Act
        var act = () => SshCredential.Create("", SshAuthMethod.PrivateKey);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*secret*required*");
    }

    [Fact]
    public void SshCredential_Create_WithNullSecret_ThrowsArgumentException()
    {
        // Act
        var act = () => SshCredential.Create(null!, SshAuthMethod.PrivateKey);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SshCredential_Equality_SameValues_AreEqual()
    {
        // Arrange
        var c1 = SshCredential.Create("secret", SshAuthMethod.PrivateKey);
        var c2 = SshCredential.Create("secret", SshAuthMethod.PrivateKey);

        // Assert
        c1.Should().Be(c2);
    }

    [Fact]
    public void SshCredential_Equality_DifferentSecret_NotEqual()
    {
        // Arrange
        var c1 = SshCredential.Create("secret1", SshAuthMethod.PrivateKey);
        var c2 = SshCredential.Create("secret2", SshAuthMethod.PrivateKey);

        // Assert
        c1.Should().NotBe(c2);
    }

    #endregion
}
