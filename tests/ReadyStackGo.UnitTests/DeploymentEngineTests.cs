using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Organizations;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Deployment;
using Xunit;

namespace ReadyStackGo.UnitTests;

/// <summary>
/// Unit tests for DeploymentEngine
/// Tests image pull error handling and local image fallback behavior
/// </summary>
public class DeploymentEngineTests
{
    private readonly Mock<IConfigStore> _configStoreMock;
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<ILogger<DeploymentEngine>> _loggerMock;
    private readonly DeploymentEngine _sut;

    public DeploymentEngineTests()
    {
        _configStoreMock = new Mock<IConfigStore>();
        _dockerServiceMock = new Mock<IDockerService>();
        _loggerMock = new Mock<ILogger<DeploymentEngine>>();

        _sut = new DeploymentEngine(
            _configStoreMock.Object,
            _dockerServiceMock.Object,
            _loggerMock.Object);

        // Setup default config responses
        SetupDefaultConfigs();
    }

    private void SetupDefaultConfigs()
    {
        var org = Organization.Create("test-org", "Test Organization");
        org.AddEnvironment(new DockerSocketEnvironment
        {
            Id = "test-env",
            Name = "Test",
            SocketPath = "/var/run/docker.sock",
            IsDefault = true
        });

        var systemConfig = new SystemConfig
        {
            Organization = org
        };

#pragma warning disable CS0618 // ContextsConfig is obsolete
        _configStoreMock.Setup(x => x.GetSystemConfigAsync()).ReturnsAsync(systemConfig);
        _configStoreMock.Setup(x => x.GetContextsConfigAsync()).ReturnsAsync(new ContextsConfig());
        _configStoreMock.Setup(x => x.GetFeaturesConfigAsync()).ReturnsAsync(new FeaturesConfig());
        _configStoreMock.Setup(x => x.GetReleaseConfigAsync()).ReturnsAsync(new ReleaseConfig());
        _configStoreMock.Setup(x => x.SaveReleaseConfigAsync(It.IsAny<ReleaseConfig>())).Returns(Task.CompletedTask);
#pragma warning restore CS0618

        // Default: no existing containers
        _dockerServiceMock.Setup(x => x.GetContainerByNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContainerDto?)null);

        // Default: network creation succeeds
        _dockerServiceMock.Setup(x => x.EnsureNetworkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: container creation succeeds
        _dockerServiceMock.Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id-123");
    }

    #region Image Pull Error Handling Tests

    [Fact]
    public async Task ExecuteDeploymentAsync_WhenImagePullFails_AndNoLocalImage_ThrowsError()
    {
        // Arrange
        var plan = CreateSimpleDeploymentPlan("nginx", "latest");

        _dockerServiceMock.Setup(x => x.PullImageAsync(It.IsAny<string>(), "nginx", "latest", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("pull access denied"));

        _dockerServiceMock.Setup(x => x.ImageExistsAsync(It.IsAny<string>(), "nginx", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Failed to pull image 'nginx:latest' and no local copy exists");
        result.Errors[0].Should().Contain("pull access denied");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WhenImagePullFails_ButLocalImageExists_UsesLocalImage()
    {
        // Arrange
        var plan = CreateSimpleDeploymentPlan("nginx", "latest");

        _dockerServiceMock.Setup(x => x.PullImageAsync(It.IsAny<string>(), "nginx", "latest", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("network unreachable"));

        _dockerServiceMock.Setup(x => x.ImageExistsAsync(It.IsAny<string>(), "nginx", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.DeployedContexts.Should().Contain("web");

        // Verify container was still created
        _dockerServiceMock.Verify(x => x.CreateAndStartContainerAsync(
            It.IsAny<string>(),
            It.Is<CreateContainerRequest>(r => r.Image == "nginx:latest"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WhenImagePullSucceeds_DoesNotCheckLocalImage()
    {
        // Arrange
        var plan = CreateSimpleDeploymentPlan("nginx", "latest");

        _dockerServiceMock.Setup(x => x.PullImageAsync(It.IsAny<string>(), "nginx", "latest", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeTrue();

        // ImageExistsAsync should not be called when pull succeeds
        _dockerServiceMock.Verify(x => x.ImageExistsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WithPrivateRegistryImage_HandlesAuthError()
    {
        // Arrange
        var plan = CreateSimpleDeploymentPlan("amssolution/myimage", "v1.0.0");

        _dockerServiceMock.Setup(x => x.PullImageAsync(It.IsAny<string>(), "amssolution/myimage", "v1.0.0", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("pull access denied for amssolution/myimage, repository does not exist or may require 'docker login'"));

        _dockerServiceMock.Setup(x => x.ImageExistsAsync(It.IsAny<string>(), "amssolution/myimage", "v1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("registry credentials are configured");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WithRegistryPort_ParsesImageCorrectly()
    {
        // Arrange - Image with registry port: registry.example.com:5000/myimage:v1
        var plan = CreateSimpleDeploymentPlan("registry.example.com:5000/myimage", "v1");

        _dockerServiceMock.Setup(x => x.PullImageAsync(
            It.IsAny<string>(),
            "registry.example.com:5000/myimage",
            "v1",
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeTrue();

        // Verify correct image name was used (not splitting on registry port)
        _dockerServiceMock.Verify(x => x.PullImageAsync(
            It.IsAny<string>(),
            "registry.example.com:5000/myimage",
            "v1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private DeploymentPlan CreateSimpleDeploymentPlan(string image, string tag)
    {
        return new DeploymentPlan
        {
            StackVersion = "test-stack",
            StackName = "test-stack",
            EnvironmentId = "test-env",
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "web",
                    Image = image,
                    Version = tag,
                    ContainerName = "test-web",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    Networks = new List<string>(),
                    DependsOn = new List<string>()
                }
            }
        };
    }

    #endregion
}
