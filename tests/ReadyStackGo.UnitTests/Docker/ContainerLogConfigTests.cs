using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ReadyStackGo.Domain.StackManagement.Stacks;
using ReadyStackGo.Infrastructure.Docker;

namespace ReadyStackGo.UnitTests.Docker;

/// <summary>
/// The default log limit that keeps a chatty container from filling the host disk,
/// and the per-service manifest override.
/// </summary>
public class ContainerLogConfigTests
{
    private static readonly DockerService.ContainerLogDefaults BuiltIn = new("10m", 3);

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    #region ContainerLogDefaults.FromConfiguration

    [Fact]
    public void FromConfiguration_NothingConfigured_UsesBuiltInDefault()
    {
        var defaults = DockerService.ContainerLogDefaults.FromConfiguration(Config());

        defaults.MaxSize.Should().Be("10m");
        defaults.MaxFile.Should().Be(3);
    }

    [Fact]
    public void FromConfiguration_ConfiguredValues_AreUsed()
    {
        var defaults = DockerService.ContainerLogDefaults.FromConfiguration(Config(
            ("Docker:ContainerLogs:MaxSize", "100m"),
            ("Docker:ContainerLogs:MaxFile", "7")));

        defaults.MaxSize.Should().Be("100m");
        defaults.MaxFile.Should().Be(7);
    }

    [Theory]
    [InlineData("ten")]
    [InlineData("0m")]
    [InlineData("-1")]
    public void FromConfiguration_InvalidValues_FallBackToBuiltIn(string invalid)
    {
        var defaults = DockerService.ContainerLogDefaults.FromConfiguration(
            Config(("Docker:ContainerLogs:MaxSize", invalid), ("Docker:ContainerLogs:MaxFile", invalid)));

        defaults.MaxSize.Should().Be("10m");
        defaults.MaxFile.Should().Be(3);
    }

    [Fact]
    public void FromConfiguration_EmptyMaxSize_SwitchesTheDefaultSizeOff()
    {
        var defaults = DockerService.ContainerLogDefaults.FromConfiguration(
            Config(("Docker:ContainerLogs:MaxSize", "")));

        defaults.MaxSize.Should().BeNull();
    }

    #endregion

    #region ResolveLogConfig

    [Fact]
    public void ResolveLogConfig_NoManifestOverride_AppliesDefault()
    {
        var config = DockerService.ResolveLogConfig(null, BuiltIn, "json-file");

        config.Should().NotBeNull();
        config!.Type.Should().Be("json-file");
        config.Config.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["max-size"] = "10m",
            ["max-file"] = "3"
        });
    }

    [Fact]
    public void ResolveLogConfig_ManifestOverridesBothFields()
    {
        var config = DockerService.ResolveLogConfig(
            new ServiceLogging { MaxSize = "50m", MaxFile = 5 }, BuiltIn, "json-file");

        config!.Config.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["max-size"] = "50m",
            ["max-file"] = "5"
        });
    }

    [Fact]
    public void ResolveLogConfig_ManifestSetsOnlyOneField_OtherKeepsDefault()
    {
        var config = DockerService.ResolveLogConfig(
            new ServiceLogging { MaxSize = "1g" }, BuiltIn, "json-file");

        config!.Config.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["max-size"] = "1g",
            ["max-file"] = "3"
        });
    }

    [Fact]
    public void ResolveLogConfig_LocalDriver_KeepsTheDaemonDriver()
    {
        var config = DockerService.ResolveLogConfig(null, BuiltIn, "local");

        config!.Type.Should().Be("local");
    }

    [Theory]
    [InlineData("journald")]
    [InlineData("syslog")]
    [InlineData(null)]
    public void ResolveLogConfig_DaemonDriverWithoutSizeRotation_NoDefaultApplied(string? driver)
    {
        // The daemon would reject max-size for these drivers and fail the deployment.
        DockerService.ResolveLogConfig(null, BuiltIn, driver).Should().BeNull();
    }

    [Fact]
    public void ResolveLogConfig_ManifestAsksForLimitOnNonRotatingDaemonDriver_UsesJsonFile()
    {
        var config = DockerService.ResolveLogConfig(
            new ServiceLogging { MaxSize = "20m" }, BuiltIn, "journald");

        config!.Type.Should().Be("json-file");
        config.Config["max-size"].Should().Be("20m");
    }

    [Fact]
    public void ResolveLogConfig_DefaultSwitchedOff_AndNoManifestOverride_ReturnsNull()
    {
        DockerService.ResolveLogConfig(null, new DockerService.ContainerLogDefaults(null, null), "json-file")
            .Should().BeNull();
    }

    [Fact]
    public void ResolveLogConfig_DefaultSwitchedOff_ManifestStillWins()
    {
        var config = DockerService.ResolveLogConfig(
            new ServiceLogging { MaxSize = "5m", MaxFile = 2 },
            new DockerService.ContainerLogDefaults(null, null), "json-file");

        config!.Config.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["max-size"] = "5m",
            ["max-file"] = "2"
        });
    }

    #endregion

    #region ServiceLogging.Validate

    [Fact]
    public void Validate_NothingSet_IsValid() =>
        ServiceLogging.Validate(null, null).Should().BeNull();

    [Theory]
    [InlineData("10m", 3)]
    [InlineData("1", 1)]
    [InlineData("300K", null)]
    public void Validate_ValidValues_ReturnNull(string maxSize, int? maxFile) =>
        ServiceLogging.Validate(maxSize, maxFile).Should().BeNull();

    [Fact]
    public void Validate_BadSize_NamesTheValue() =>
        ServiceLogging.Validate("big", null).Should().Contain("'big'");

    [Fact]
    public void Validate_ZeroFiles_IsInvalid() =>
        ServiceLogging.Validate(null, 0).Should().Contain("maxFile");

    #endregion
}
