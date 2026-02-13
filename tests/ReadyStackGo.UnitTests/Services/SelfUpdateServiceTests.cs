using Docker.DotNet.Models;
using FluentAssertions;
using ReadyStackGo.Infrastructure.Docker;

namespace ReadyStackGo.UnitTests.Services;

/// <summary>
/// Unit tests for SelfUpdateService.
/// Tests container config mapping and static helper methods.
/// Docker API calls are not tested here (require integration tests).
/// </summary>
public class SelfUpdateServiceTests
{
    [Fact]
    public void BuildCreateParamsFromInspection_PreservesEnvVars()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.Config.Env = new List<string>
        {
            "ASPNETCORE_ENVIRONMENT=Production",
            "ConnectionStrings__ReadyStackGo=Data Source=/app/data/readystackgo.db"
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.Env.Should().BeEquivalentTo(inspection.Config.Env);
    }

    [Fact]
    public void BuildCreateParamsFromInspection_PreservesPortBindings()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.HostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>
        {
            ["8080/tcp"] = new List<PortBinding> { new() { HostPort = "8080" } },
            ["8443/tcp"] = new List<PortBinding> { new() { HostPort = "8443" } }
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.HostConfig.PortBindings.Should().HaveCount(2);
        result.HostConfig.PortBindings["8080/tcp"].First().HostPort.Should().Be("8080");
        result.HostConfig.PortBindings["8443/tcp"].First().HostPort.Should().Be("8443");
    }

    [Fact]
    public void BuildCreateParamsFromInspection_PreservesVolumeBinds()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.HostConfig.Binds = new List<string>
        {
            "/var/run/docker.sock:/var/run/docker.sock",
            "rsgo-config:/app/config",
            "rsgo-data:/app/data"
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.HostConfig.Binds.Should().BeEquivalentTo(inspection.HostConfig.Binds);
    }

    [Fact]
    public void BuildCreateParamsFromInspection_PreservesRestartPolicy()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.HostConfig.RestartPolicy = new RestartPolicy
        {
            Name = RestartPolicyKind.UnlessStopped
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.HostConfig.RestartPolicy.Name.Should().Be(RestartPolicyKind.UnlessStopped);
    }

    [Fact]
    public void BuildCreateParamsFromInspection_UsesNewImage()
    {
        // Arrange
        var inspection = CreateMinimalInspection();

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:2.0.0", "readystackgo-update");

        // Assert
        result.Image.Should().Be("wiesenwischer/readystackgo:2.0.0");
    }

    [Fact]
    public void BuildCreateParamsFromInspection_UsesNewName()
    {
        // Arrange
        var inspection = CreateMinimalInspection();

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:2.0.0", "readystackgo-update");

        // Assert
        result.Name.Should().Be("readystackgo-update");
    }

    [Fact]
    public void BuildCreateParamsFromInspection_PreservesNetworkMode()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.HostConfig.NetworkMode = "rsgo-net";

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.HostConfig.NetworkMode.Should().Be("rsgo-net");
    }

    [Fact]
    public void BuildCreateParamsFromInspection_PreservesLabels()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.Config.Labels = new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = "readystackgo"
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.Labels.Should().ContainKey("com.docker.compose.project");
    }

    [Fact]
    public void BuildCreateParamsFromInspection_PreservesExposedPorts()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.Config.ExposedPorts = new Dictionary<string, EmptyStruct>
        {
            ["8080/tcp"] = default,
            ["8443/tcp"] = default
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.ExposedPorts.Should().HaveCount(2);
    }

    [Fact]
    public void BuildCreateParamsFromInspection_PreservesNetworkAliases()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.HostConfig.NetworkMode = "rsgo-net";
        inspection.NetworkSettings = new NetworkSettings
        {
            Networks = new Dictionary<string, EndpointSettings>
            {
                ["rsgo-net"] = new EndpointSettings
                {
                    Aliases = new List<string> { "readystackgo" }
                }
            }
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.NetworkingConfig.Should().NotBeNull();
        result.NetworkingConfig!.EndpointsConfig["rsgo-net"].Aliases.Should().Contain("readystackgo");
    }

    [Fact]
    public void BuildCreateParamsFromInspection_NullNetworkSettings_NoNetworkingConfig()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.NetworkSettings = null!;

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update");

        // Assert
        result.NetworkingConfig.Should().BeNull();
    }

    private static ContainerInspectResponse CreateMinimalInspection()
    {
        return new ContainerInspectResponse
        {
            Name = "/readystackgo",
            Config = new Config
            {
                Image = "wiesenwischer/readystackgo:latest",
                Env = new List<string>(),
                Labels = new Dictionary<string, string>(),
                ExposedPorts = new Dictionary<string, EmptyStruct>()
            },
            HostConfig = new HostConfig
            {
                Binds = new List<string>(),
                PortBindings = new Dictionary<string, IList<PortBinding>>(),
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.No },
                NetworkMode = "bridge"
            },
            NetworkSettings = new NetworkSettings
            {
                Networks = new Dictionary<string, EndpointSettings>()
            }
        };
    }
}
