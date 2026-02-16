using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Volumes;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

public class DockerVolumeTests
{
    #region Factory Method – FromDockerApi

    [Fact]
    public void FromDockerApi_ValidData_CreatesVolume()
    {
        var volume = DockerVolume.FromDockerApi("my-volume", "local", "/mnt/data", "local",
            new DateTime(2025, 1, 1), new Dictionary<string, string> { ["app"] = "test" });

        volume.Name.Should().Be("my-volume");
        volume.Driver.Should().Be("local");
        volume.Mountpoint.Should().Be("/mnt/data");
        volume.Scope.Should().Be("local");
        volume.CreatedAt.Should().Be(new DateTime(2025, 1, 1));
        volume.Labels.Should().ContainKey("app").WhoseValue.Should().Be("test");
    }

    [Fact]
    public void FromDockerApi_MinimalData_UsesDefaults()
    {
        var volume = DockerVolume.FromDockerApi("vol", "local");

        volume.Name.Should().Be("vol");
        volume.Driver.Should().Be("local");
        volume.Mountpoint.Should().BeNull();
        volume.Scope.Should().BeNull();
        volume.CreatedAt.Should().BeNull();
        volume.Labels.Should().BeEmpty();
    }

    [Fact]
    public void FromDockerApi_NullDriver_DefaultsToLocal()
    {
        var volume = DockerVolume.FromDockerApi("vol", null!);

        volume.Driver.Should().Be("local");
    }

    [Fact]
    public void FromDockerApi_NullLabels_CreatesEmptyDictionary()
    {
        var volume = DockerVolume.FromDockerApi("vol", "local", labels: null);

        volume.Labels.Should().NotBeNull();
        volume.Labels.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromDockerApi_EmptyOrWhitespaceName_ThrowsArgumentException(string? name)
    {
        var act = () => DockerVolume.FromDockerApi(name!, "local");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Volume name cannot be empty*");
    }

    [Fact]
    public void FromDockerApi_LabelsAreCopied_NotShared()
    {
        var originalLabels = new Dictionary<string, string> { ["key"] = "value" };
        var volume = DockerVolume.FromDockerApi("vol", "local", labels: originalLabels);

        originalLabels["key"] = "changed";

        volume.Labels["key"].Should().Be("value");
    }

    #endregion

    #region IsOrphaned

    [Fact]
    public void IsOrphaned_NoReferences_ReturnsTrue()
    {
        var volume = DockerVolume.FromDockerApi("orphan-vol", "local");
        var refs = new List<VolumeReference>();

        volume.IsOrphaned(refs).Should().BeTrue();
    }

    [Fact]
    public void IsOrphaned_ReferencedByContainer_ReturnsFalse()
    {
        var volume = DockerVolume.FromDockerApi("my-vol", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "app", VolumeName = "my-vol", MountPath = "/data" }
        };

        volume.IsOrphaned(refs).Should().BeFalse();
    }

    [Fact]
    public void IsOrphaned_CaseInsensitiveMatch_ReturnsFalse()
    {
        var volume = DockerVolume.FromDockerApi("My-Volume", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "app", VolumeName = "my-volume", MountPath = "/data" }
        };

        volume.IsOrphaned(refs).Should().BeFalse();
    }

    [Fact]
    public void IsOrphaned_DifferentVolumeReferenced_ReturnsTrue()
    {
        var volume = DockerVolume.FromDockerApi("vol-a", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "app", VolumeName = "vol-b", MountPath = "/data" }
        };

        volume.IsOrphaned(refs).Should().BeTrue();
    }

    [Fact]
    public void IsOrphaned_MultipleReferencesOnlyOtherVolumes_ReturnsTrue()
    {
        var volume = DockerVolume.FromDockerApi("target-vol", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "app1", VolumeName = "other-vol-1", MountPath = "/data1" },
            new() { ContainerName = "app2", VolumeName = "other-vol-2", MountPath = "/data2" },
        };

        volume.IsOrphaned(refs).Should().BeTrue();
    }

    #endregion

    #region GetReferencingContainers

    [Fact]
    public void GetReferencingContainers_NoReferences_ReturnsEmpty()
    {
        var volume = DockerVolume.FromDockerApi("vol", "local");
        var refs = new List<VolumeReference>();

        volume.GetReferencingContainers(refs).Should().BeEmpty();
    }

    [Fact]
    public void GetReferencingContainers_OneContainer_ReturnsSingleItem()
    {
        var volume = DockerVolume.FromDockerApi("vol", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "app", VolumeName = "vol", MountPath = "/data" }
        };

        var result = volume.GetReferencingContainers(refs);

        result.Should().ContainSingle().Which.Should().Be("app");
    }

    [Fact]
    public void GetReferencingContainers_DuplicateContainerName_ReturnsDistinct()
    {
        var volume = DockerVolume.FromDockerApi("vol", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "app", VolumeName = "vol", MountPath = "/data1" },
            new() { ContainerName = "app", VolumeName = "vol", MountPath = "/data2" },
        };

        var result = volume.GetReferencingContainers(refs);

        result.Should().ContainSingle().Which.Should().Be("app");
    }

    [Fact]
    public void GetReferencingContainers_CaseInsensitiveDuplicates_ReturnsDistinct()
    {
        var volume = DockerVolume.FromDockerApi("vol", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "App", VolumeName = "vol", MountPath = "/data1" },
            new() { ContainerName = "app", VolumeName = "vol", MountPath = "/data2" },
        };

        var result = volume.GetReferencingContainers(refs);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void GetReferencingContainers_MultipleContainers_ReturnsAll()
    {
        var volume = DockerVolume.FromDockerApi("shared-vol", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "app-1", VolumeName = "shared-vol", MountPath = "/data" },
            new() { ContainerName = "app-2", VolumeName = "shared-vol", MountPath = "/backup" },
            new() { ContainerName = "app-3", VolumeName = "other-vol", MountPath = "/data" },
        };

        var result = volume.GetReferencingContainers(refs);

        result.Should().HaveCount(2);
        result.Should().Contain("app-1");
        result.Should().Contain("app-2");
    }

    [Fact]
    public void GetReferencingContainers_CaseInsensitiveVolumeName_MatchesCorrectly()
    {
        var volume = DockerVolume.FromDockerApi("My-Volume", "local");
        var refs = new List<VolumeReference>
        {
            new() { ContainerName = "app", VolumeName = "my-volume", MountPath = "/data" }
        };

        volume.GetReferencingContainers(refs).Should().ContainSingle().Which.Should().Be("app");
    }

    #endregion
}
