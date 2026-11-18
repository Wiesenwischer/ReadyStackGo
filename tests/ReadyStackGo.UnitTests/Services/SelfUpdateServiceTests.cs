using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Docker;

namespace ReadyStackGo.UnitTests.Services;

/// <summary>
/// Unit tests for SelfUpdateService.
/// Tests container config mapping and static helper methods.
/// Docker API calls are not tested here (require integration tests).
/// </summary>
public class SelfUpdateServiceTests
{
    private static readonly DockerService.ContainerLogDefaults LogDefaults = new("10m", 3);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

        // Assert
        result.HostConfig.RestartPolicy.Name.Should().Be(RestartPolicyKind.UnlessStopped);
    }

    [Fact]
    public void BuildCreateParamsFromInspection_PreservesLogConfig()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.HostConfig.LogConfig = new LogConfig
        {
            Type = "json-file",
            Config = new Dictionary<string, string>
            {
                ["max-size"] = "10m",
                ["max-file"] = "3"
            }
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

        // Assert
        result.HostConfig.LogConfig.Type.Should().Be("json-file");
        result.HostConfig.LogConfig.Config.Should().BeEquivalentTo(inspection.HostConfig.LogConfig.Config);
    }

    [Fact]
    public void BuildCreateParamsFromInspection_WithoutLogConfig_DoesNotInventOne()
    {
        // Arrange
        var inspection = CreateMinimalInspection();
        inspection.HostConfig.LogConfig = null;

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

        // Assert
        result.HostConfig.LogConfig.Should().BeNull();
    }

    [Fact]
    public void BuildCreateParamsFromInspection_JsonFileWithoutSizeLimit_GetsDefaultLimit()
    {
        // Arrange: what Docker reports for a container created without --log-opt (every install before #513)
        var inspection = CreateMinimalInspection();
        inspection.HostConfig.LogConfig = new LogConfig
        {
            Type = "json-file",
            Config = new Dictionary<string, string>()
        };

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

        // Assert
        result.HostConfig.LogConfig.Type.Should().Be("json-file");
        result.HostConfig.LogConfig.Config.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["max-size"] = "10m",
            ["max-file"] = "3"
        });
        inspection.HostConfig.LogConfig.Config.Should().BeEmpty("the inspected config must not be mutated");
    }

    #region CarryOverLogConfig

    [Fact]
    public void CarryOverLogConfig_JsonFileWithNullOptions_GetsDefaultLimit()
    {
        var current = new LogConfig { Type = "json-file", Config = null };

        var result = SelfUpdateService.CarryOverLogConfig(current, LogDefaults);

        result!.Type.Should().Be("json-file");
        result.Config.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["max-size"] = "10m",
            ["max-file"] = "3"
        });
    }

    [Fact]
    public void CarryOverLogConfig_LocalDriverWithoutSizeLimit_GetsDefaultLimit()
    {
        var current = new LogConfig { Type = "local", Config = new Dictionary<string, string>() };

        var result = SelfUpdateService.CarryOverLogConfig(current, LogDefaults);

        result!.Type.Should().Be("local");
        result.Config.Should().ContainKey("max-size").WhoseValue.Should().Be("10m");
        result.Config.Should().ContainKey("max-file").WhoseValue.Should().Be("3");
    }

    [Fact]
    public void CarryOverLogConfig_ExistingSizeLimit_IsKeptUnchanged()
    {
        var current = new LogConfig
        {
            Type = "json-file",
            Config = new Dictionary<string, string> { ["max-size"] = "50m" }
        };

        var result = SelfUpdateService.CarryOverLogConfig(current, LogDefaults);

        result.Should().BeSameAs(current);
        result!.Config.Should().NotContainKey("max-file",
            "an operator-chosen limit is not topped up with the default file count");
    }

    [Fact]
    public void CarryOverLogConfig_OnlyMaxFileSet_AddsSizeAndKeepsFileCount()
    {
        // max-file has no effect on json-file without max-size, so the log is still unlimited
        var current = new LogConfig
        {
            Type = "json-file",
            Config = new Dictionary<string, string> { ["max-file"] = "5" }
        };

        var result = SelfUpdateService.CarryOverLogConfig(current, LogDefaults);

        result!.Config.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["max-size"] = "10m",
            ["max-file"] = "5"
        });
    }

    [Fact]
    public void CarryOverLogConfig_OtherOptions_AreKept()
    {
        var current = new LogConfig
        {
            Type = "json-file",
            Config = new Dictionary<string, string> { ["labels"] = "env", ["compress"] = "true" }
        };

        var result = SelfUpdateService.CarryOverLogConfig(current, LogDefaults);

        result!.Config.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["labels"] = "env",
            ["compress"] = "true",
            ["max-size"] = "10m",
            ["max-file"] = "3"
        });
    }

    [Theory]
    [InlineData("journald")]
    [InlineData("syslog")]
    [InlineData("none")]
    [InlineData("")]
    public void CarryOverLogConfig_DriverWithoutSizeRotation_IsKeptUnchanged(string driver)
    {
        // These drivers reject max-size; adding it would make the container creation fail
        var current = new LogConfig { Type = driver, Config = new Dictionary<string, string>() };

        var result = SelfUpdateService.CarryOverLogConfig(current, LogDefaults);

        result.Should().BeSameAs(current);
        result!.Config.Should().BeEmpty();
    }

    [Fact]
    public void CarryOverLogConfig_NullConfig_StaysNull()
    {
        SelfUpdateService.CarryOverLogConfig(null, LogDefaults).Should().BeNull();
    }

    [Fact]
    public void CarryOverLogConfig_DefaultSwitchedOff_IsKeptUnchanged()
    {
        // Docker:ContainerLogs:MaxSize set to "" turns the default off
        var current = new LogConfig { Type = "json-file", Config = new Dictionary<string, string>() };

        var result = SelfUpdateService.CarryOverLogConfig(current, new DockerService.ContainerLogDefaults(null, 3));

        result.Should().BeSameAs(current);
        result!.Config.Should().BeEmpty();
    }

    [Fact]
    public void CarryOverLogConfig_DefaultWithoutFileCount_AddsOnlySize()
    {
        var current = new LogConfig { Type = "json-file", Config = new Dictionary<string, string>() };

        var result = SelfUpdateService.CarryOverLogConfig(current, new DockerService.ContainerLogDefaults("20m", null));

        result!.Config.Should().BeEquivalentTo(new Dictionary<string, string> { ["max-size"] = "20m" });
    }

    #endregion

    [Fact]
    public void BuildCreateParamsFromInspection_UsesNewImage()
    {
        // Arrange
        var inspection = CreateMinimalInspection();

        // Act
        var result = SelfUpdateService.BuildCreateParamsFromInspection(
            inspection, "wiesenwischer/readystackgo:2.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:2.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

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
            inspection, "wiesenwischer/readystackgo:1.0.0", "readystackgo-update", LogDefaults);

        // Assert
        result.NetworkingConfig.Should().BeNull();
    }

    [Theory]
    [InlineData("wiesenwischer/readystackgo", "https://index.docker.io/v1/")]
    [InlineData("library/nginx", "https://index.docker.io/v1/")]
    [InlineData("myregistry.local/repo/image", "https://myregistry.local")]
    [InlineData("myregistry.local:5000/repo/image", "https://myregistry.local:5000")]
    [InlineData("ghcr.io/owner/image", "https://ghcr.io")]
    public void GetRegistryFromImage_ExtractsCorrectRegistry(string imageName, string expected)
    {
        var result = SelfUpdateService.GetRegistryFromImage(imageName);
        result.Should().Be(expected);
    }

    [Fact]
    public void ExtractPullError_UsesStructuredErrorMessage()
    {
        var msg = new JSONMessage { Error = new JSONError { Code = 1, Message = " write /var/lib/docker/tmp/x: no space left on device " } };

        SelfUpdateService.ExtractPullError(msg).Should().Be("write /var/lib/docker/tmp/x: no space left on device");
    }

    [Fact]
    public void ExtractPullError_FallsBackToErrorMessageText()
    {
        var msg = new JSONMessage { ErrorMessage = "pull access denied for wiesenwischer/readystackgo" };

        SelfUpdateService.ExtractPullError(msg).Should().Be("pull access denied for wiesenwischer/readystackgo");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractPullError_ProgressMessage_ReturnsNull(string? status)
    {
        var msg = new JSONMessage { Status = status, ErrorMessage = status };

        SelfUpdateService.ExtractPullError(msg).Should().BeNull();
    }

    [Fact]
    public void BuildErrorProgress_ImagePullFailed_ShowsShortMessageAndRealReasonInDetails()
    {
        var ex = new ImagePullFailedException("wiesenwischer/readystackgo:0.85.0", "write /var/lib/docker/x: no space left on device");

        var progress = SelfUpdateService.BuildErrorProgress(ex, 180);

        progress.Phase.Should().Be("error");
        progress.Message.Should().Be("Could not download wiesenwischer/readystackgo:0.85.0. The previous version is still running.");
        progress.Message.Should().NotContain("No such image");
        progress.Details.Should().Contain("Image: wiesenwischer/readystackgo:0.85.0");
        progress.Details.Should().Contain("no space left on device");
        progress.Details.Should().Contain("disk of the Docker host is full");
    }

    [Fact]
    public void BuildErrorProgress_ImagePullFailed_OtherReason_HasNoDiskHint()
    {
        var ex = new ImagePullFailedException("wiesenwischer/readystackgo:0.85.0", "pull access denied");

        var progress = SelfUpdateService.BuildErrorProgress(ex, 180);

        progress.Details.Should().Contain("pull access denied");
        progress.Details.Should().NotContain("disk");
    }

    [Fact]
    public void BuildErrorProgress_Stalled_NamesTheTimeout()
    {
        var progress = SelfUpdateService.BuildErrorProgress(new OperationCanceledException(), 180);

        progress.Message.Should().Be("Download stalled (no progress for 3 min). Please try again.");
        progress.Details.Should().Contain("180s");
    }

    [Fact]
    public void BuildErrorProgress_DockerApiError_CarriesStatusAndBody()
    {
        var ex = new DockerApiException(System.Net.HttpStatusCode.InternalServerError, "{\"message\":\"boom\"}");

        var progress = SelfUpdateService.BuildErrorProgress(ex, 180);

        progress.Message.Should().StartWith("Docker error:");
        progress.Details.Should().Contain("500").And.Contain("boom");
    }

    [Fact]
    public void BuildErrorProgress_UnexpectedException_PutsStackTraceInDetails()
    {
        var progress = SelfUpdateService.BuildErrorProgress(new InvalidOperationException("kaputt"), 180);

        progress.Message.Should().Be("Update failed: kaputt");
        progress.Details.Should().Contain("InvalidOperationException").And.Contain("kaputt");
    }

    [Fact]
    public void UpdateProgress_Idle_HasNoDetails()
    {
        UpdateProgress.Idle.Details.Should().BeNull();
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
