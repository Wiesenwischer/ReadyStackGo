using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Deployments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for DeployedService entity.
/// </summary>
public class DeployedServiceTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_CreatesDeployedService()
    {
        // Act
        var service = new DeployedService(
            serviceName: "web",
            containerId: "abc123",
            containerName: "myapp_web_1",
            image: "nginx:latest",
            status: "running");

        // Assert
        service.ServiceName.Should().Be("web");
        service.ContainerId.Should().Be("abc123");
        service.ContainerName.Should().Be("myapp_web_1");
        service.Image.Should().Be("nginx:latest");
        service.Status.Should().Be("running");
        service.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithNullOptionalParameters_CreatesDeployedService()
    {
        // Act
        var service = new DeployedService(
            serviceName: "db",
            containerId: null,
            containerName: null,
            image: null,
            status: "pending");

        // Assert
        service.ServiceName.Should().Be("db");
        service.ContainerId.Should().BeNull();
        service.ContainerName.Should().BeNull();
        service.Image.Should().BeNull();
        service.Status.Should().Be("pending");
    }

    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        // Act
        var service1 = new DeployedService("web", null, null, null, "running");
        var service2 = new DeployedService("web", null, null, null, "running");

        // Assert
        service1.Id.Should().NotBe(service2.Id);
    }

    #endregion

    #region UpdateStatus Tests

    [Fact]
    public void UpdateStatus_ChangesStatus()
    {
        // Arrange
        var service = new DeployedService("web", "abc123", "myapp_web_1", "nginx:latest", "running");

        // Act
        service.UpdateStatus("stopped");

        // Assert
        service.Status.Should().Be("stopped");
    }

    [Theory]
    [InlineData("running")]
    [InlineData("stopped")]
    [InlineData("starting")]
    [InlineData("removing")]
    [InlineData("exited")]
    public void UpdateStatus_AcceptsVariousStatuses(string status)
    {
        // Arrange
        var service = new DeployedService("web", null, null, null, "pending");

        // Act
        service.UpdateStatus(status);

        // Assert
        service.Status.Should().Be(status);
    }

    #endregion

    #region UpdateContainerInfo Tests

    [Fact]
    public void UpdateContainerInfo_UpdatesContainerIdAndName()
    {
        // Arrange
        var service = new DeployedService("web", null, null, "nginx:latest", "starting");

        // Act
        service.UpdateContainerInfo("container123", "myapp_web_1");

        // Assert
        service.ContainerId.Should().Be("container123");
        service.ContainerName.Should().Be("myapp_web_1");
    }

    [Fact]
    public void UpdateContainerInfo_OverwritesExistingValues()
    {
        // Arrange
        var service = new DeployedService("web", "old-id", "old-name", "nginx:latest", "running");

        // Act
        service.UpdateContainerInfo("new-id", "new-name");

        // Assert
        service.ContainerId.Should().Be("new-id");
        service.ContainerName.Should().Be("new-name");
    }

    [Fact]
    public void UpdateContainerInfo_DoesNotAffectOtherProperties()
    {
        // Arrange
        var service = new DeployedService("web", "abc123", "myapp_web_1", "nginx:latest", "running");
        var originalId = service.Id;

        // Act
        service.UpdateContainerInfo("new-id", "new-name");

        // Assert
        service.Id.Should().Be(originalId);
        service.ServiceName.Should().Be("web");
        service.Image.Should().Be("nginx:latest");
        service.Status.Should().Be("running");
    }

    #endregion
}
