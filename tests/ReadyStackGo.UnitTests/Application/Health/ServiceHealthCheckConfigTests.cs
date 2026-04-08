using FluentAssertions;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.UnitTests.Application.Health;

/// <summary>
/// Tests for ServiceHealthCheckConfig factory methods and type detection properties.
/// </summary>
public class ServiceHealthCheckConfigTests
{
    [Fact]
    public void Http_Factory_SetsTypeCorrectly()
    {
        var config = ServiceHealthCheckConfig.Http("/health", 8080);

        config.Type.Should().Be("http");
        config.IsHttp.Should().BeTrue();
        config.IsTcp.Should().BeFalse();
        config.IsDisabled.Should().BeFalse();
        config.Path.Should().Be("/health");
        config.Port.Should().Be(8080);
    }

    [Fact]
    public void Tcp_Factory_SetsTypeCorrectly()
    {
        var config = ServiceHealthCheckConfig.Tcp(6379, 3);

        config.Type.Should().Be("tcp");
        config.IsTcp.Should().BeTrue();
        config.IsHttp.Should().BeFalse();
        config.IsDisabled.Should().BeFalse();
        config.Port.Should().Be(6379);
        config.TimeoutSeconds.Should().Be(3);
    }

    [Fact]
    public void Tcp_Factory_DefaultValues()
    {
        var config = ServiceHealthCheckConfig.Tcp();

        config.Type.Should().Be("tcp");
        config.IsTcp.Should().BeTrue();
        config.Port.Should().BeNull();
        config.TimeoutSeconds.Should().Be(5);
    }

    [Fact]
    public void Docker_Factory_SetsTypeCorrectly()
    {
        var config = ServiceHealthCheckConfig.Docker();

        config.Type.Should().Be("docker");
        config.IsHttp.Should().BeFalse();
        config.IsTcp.Should().BeFalse();
        config.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public void None_Factory_SetsTypeCorrectly()
    {
        var config = ServiceHealthCheckConfig.None();

        config.Type.Should().Be("none");
        config.IsDisabled.Should().BeTrue();
        config.IsHttp.Should().BeFalse();
        config.IsTcp.Should().BeFalse();
    }

    [Theory]
    [InlineData("tcp", true)]
    [InlineData("TCP", true)]
    [InlineData("Tcp", true)]
    [InlineData("http", false)]
    [InlineData("docker", false)]
    [InlineData("none", false)]
    public void IsTcp_CaseInsensitive(string type, bool expected)
    {
        var config = new ServiceHealthCheckConfig { Type = type };
        config.IsTcp.Should().Be(expected);
    }
}
