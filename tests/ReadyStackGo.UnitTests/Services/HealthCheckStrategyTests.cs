using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.Services;

public class HealthCheckStrategyFactoryTests
{
    [Fact]
    public void GetStrategy_Http_ReturnsHttpStrategy()
    {
        var httpChecker = new Mock<IHttpHealthChecker>();
        var logger = new Mock<ILogger<HttpHealthCheckStrategy>>();
        var httpStrategy = new HttpHealthCheckStrategy(httpChecker.Object, logger.Object);

        var factory = new HealthCheckStrategyFactory(new IHealthCheckStrategy[] { httpStrategy });

        factory.GetStrategy("http").Should().BeSameAs(httpStrategy);
    }

    [Fact]
    public void GetStrategy_Tcp_ReturnsTcpStrategy()
    {
        var tcpChecker = new Mock<ITcpHealthChecker>();
        var logger = new Mock<ILogger<TcpHealthCheckStrategy>>();
        var tcpStrategy = new TcpHealthCheckStrategy(tcpChecker.Object, logger.Object);

        var factory = new HealthCheckStrategyFactory(new IHealthCheckStrategy[] { tcpStrategy });

        factory.GetStrategy("tcp").Should().BeSameAs(tcpStrategy);
    }

    [Fact]
    public void GetStrategy_Docker_ReturnsDockerStrategy()
    {
        var dockerStrategy = new DockerHealthCheckStrategy();
        var factory = new HealthCheckStrategyFactory(new IHealthCheckStrategy[] { dockerStrategy });

        factory.GetStrategy("docker").Should().BeSameAs(dockerStrategy);
    }

    [Fact]
    public void GetStrategy_UnknownType_ReturnsFallbackDockerStrategy()
    {
        var factory = new HealthCheckStrategyFactory(Array.Empty<IHealthCheckStrategy>());

        var strategy = factory.GetStrategy("unknown");

        strategy.Should().BeOfType<DockerHealthCheckStrategy>();
    }

    [Fact]
    public void GetStrategy_CaseInsensitive()
    {
        var tcpChecker = new Mock<ITcpHealthChecker>();
        var logger = new Mock<ILogger<TcpHealthCheckStrategy>>();
        var tcpStrategy = new TcpHealthCheckStrategy(tcpChecker.Object, logger.Object);

        var factory = new HealthCheckStrategyFactory(new IHealthCheckStrategy[] { tcpStrategy });

        factory.GetStrategy("TCP").Should().BeSameAs(tcpStrategy);
        factory.GetStrategy("Tcp").Should().BeSameAs(tcpStrategy);
    }

    [Fact]
    public void GetStrategy_None_ReturnsFallbackDockerStrategy()
    {
        var factory = new HealthCheckStrategyFactory(Array.Empty<IHealthCheckStrategy>());

        var strategy = factory.GetStrategy("none");

        strategy.Should().BeOfType<DockerHealthCheckStrategy>();
    }
}

public class DockerHealthCheckStrategyTests
{
    [Fact]
    public void SupportedType_IsDocker()
    {
        new DockerHealthCheckStrategy().SupportedType.Should().Be("docker");
    }

    [Fact]
    public async Task CheckHealth_RunningContainer_WithHealthy_ReturnsHealthy()
    {
        var container = CreateContainer("running", "healthy");
        var strategy = new DockerHealthCheckStrategy();

        var result = await strategy.CheckHealthAsync(
            container, "test-svc", ServiceHealthCheckConfig.Docker(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealth_RunningContainer_NoHealthCheck_ReturnsRunning()
    {
        var container = CreateContainer("running", "");
        var strategy = new DockerHealthCheckStrategy();

        var result = await strategy.CheckHealthAsync(
            container, "test-svc", ServiceHealthCheckConfig.Docker(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Running);
    }

    [Fact]
    public async Task CheckHealth_ExitedContainer_ReturnsUnhealthy()
    {
        var container = CreateContainer("exited", "");
        var strategy = new DockerHealthCheckStrategy();

        var result = await strategy.CheckHealthAsync(
            container, "test-svc", ServiceHealthCheckConfig.Docker(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void FromDocker_UnhealthyContainer_IncludesFailingStreak()
    {
        var container = CreateContainer("running", "unhealthy", failingStreak: 5);

        var result = DockerHealthCheckStrategy.FromDocker(container);

        result.Reason.Should().Contain("streak: 5");
    }

    private static ContainerDto CreateContainer(string state, string healthStatus, int failingStreak = 0) => new()
    {
        Id = "abc123",
        Name = "/test-container",
        Image = "test:latest",
        State = state,
        Status = "Up 1 hour",
        Created = DateTime.UtcNow,
        HealthStatus = healthStatus,
        FailingStreak = failingStreak,
        Labels = new Dictionary<string, string>()
    };
}

public class TcpHealthCheckStrategyTests
{
    [Fact]
    public void SupportedType_IsTcp()
    {
        var checker = new Mock<ITcpHealthChecker>();
        var logger = new Mock<ILogger<TcpHealthCheckStrategy>>();

        new TcpHealthCheckStrategy(checker.Object, logger.Object).SupportedType.Should().Be("tcp");
    }

    [Fact]
    public async Task CheckHealth_HealthyResult_ReturnsHealthy()
    {
        var checker = new Mock<ITcpHealthChecker>();
        checker.Setup(c => c.CheckHealthAsync(It.IsAny<string>(), It.IsAny<TcpHealthCheckConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TcpHealthCheckResult.Healthy(42));

        var logger = new Mock<ILogger<TcpHealthCheckStrategy>>();
        var strategy = new TcpHealthCheckStrategy(checker.Object, logger.Object);

        var container = CreateRunningContainer(port: 6379);
        var config = ServiceHealthCheckConfig.Tcp(port: 6379);

        var result = await strategy.CheckHealthAsync(container, "redis", config, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.ResponseTimeMs.Should().Be(42);
    }

    [Fact]
    public async Task CheckHealth_UnhealthyResult_ReturnsUnhealthy()
    {
        var checker = new Mock<ITcpHealthChecker>();
        checker.Setup(c => c.CheckHealthAsync(It.IsAny<string>(), It.IsAny<TcpHealthCheckConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TcpHealthCheckResult.ConnectionFailed("Connection refused"));

        var logger = new Mock<ILogger<TcpHealthCheckStrategy>>();
        var strategy = new TcpHealthCheckStrategy(checker.Object, logger.Object);

        var container = CreateRunningContainer(port: 6379);
        var config = ServiceHealthCheckConfig.Tcp(port: 6379);

        var result = await strategy.CheckHealthAsync(container, "redis", config, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Reason.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task CheckHealth_NoPort_FallsBackToDocker()
    {
        var checker = new Mock<ITcpHealthChecker>();
        var logger = new Mock<ILogger<TcpHealthCheckStrategy>>();
        var strategy = new TcpHealthCheckStrategy(checker.Object, logger.Object);

        // Container with no ports and config with no port
        var container = CreateRunningContainer(port: null);
        var config = ServiceHealthCheckConfig.Tcp();

        var result = await strategy.CheckHealthAsync(container, "redis", config, CancellationToken.None);

        // Should fall back to Docker status (Running since container is running with no HEALTHCHECK)
        result.Status.Should().Be(HealthStatus.Running);
        checker.Verify(c => c.CheckHealthAsync(It.IsAny<string>(), It.IsAny<TcpHealthCheckConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckHealth_UsesFirstExposedPort_WhenConfigPortIsNull()
    {
        var checker = new Mock<ITcpHealthChecker>();
        checker.Setup(c => c.CheckHealthAsync(It.IsAny<string>(), It.Is<TcpHealthCheckConfig>(cfg => cfg.Port == 6379), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TcpHealthCheckResult.Healthy(10));

        var logger = new Mock<ILogger<TcpHealthCheckStrategy>>();
        var strategy = new TcpHealthCheckStrategy(checker.Object, logger.Object);

        var container = CreateRunningContainer(port: 6379);
        var config = ServiceHealthCheckConfig.Tcp(); // No port specified

        var result = await strategy.CheckHealthAsync(container, "redis", config, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        checker.Verify(c => c.CheckHealthAsync(It.IsAny<string>(), It.Is<TcpHealthCheckConfig>(cfg => cfg.Port == 6379), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ContainerDto CreateRunningContainer(int? port) => new()
    {
        Id = "abc123",
        Name = "/test-redis",
        Image = "redis:alpine",
        State = "running",
        Status = "Up 1 hour",
        Created = DateTime.UtcNow,
        HealthStatus = "",
        Labels = new Dictionary<string, string>(),
        Ports = port.HasValue
            ? new List<PortDto> { new() { PrivatePort = port.Value, PublicPort = port.Value, Type = "tcp" } }
            : new List<PortDto>()
    };
}

public class ServiceHealthCheckConfigTests
{
    [Fact]
    public void Tcp_Factory_CreatesCorrectConfig()
    {
        var config = ServiceHealthCheckConfig.Tcp(port: 6379, timeoutSeconds: 3);

        config.Type.Should().Be("tcp");
        config.Port.Should().Be(6379);
        config.TimeoutSeconds.Should().Be(3);
        config.IsTcp.Should().BeTrue();
        config.IsHttp.Should().BeFalse();
        config.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public void Tcp_Factory_DefaultValues()
    {
        var config = ServiceHealthCheckConfig.Tcp();

        config.Port.Should().BeNull();
        config.TimeoutSeconds.Should().Be(5);
    }

    [Fact]
    public void IsTcp_ReturnsTrueForTcpType()
    {
        var config = new ServiceHealthCheckConfig { Type = "tcp" };
        config.IsTcp.Should().BeTrue();
    }

    [Fact]
    public void IsTcp_CaseInsensitive()
    {
        var config = new ServiceHealthCheckConfig { Type = "TCP" };
        config.IsTcp.Should().BeTrue();
    }

    [Fact]
    public void IsTcp_ReturnsFalseForOtherTypes()
    {
        new ServiceHealthCheckConfig { Type = "http" }.IsTcp.Should().BeFalse();
        new ServiceHealthCheckConfig { Type = "docker" }.IsTcp.Should().BeFalse();
        new ServiceHealthCheckConfig { Type = "none" }.IsTcp.Should().BeFalse();
    }
}
