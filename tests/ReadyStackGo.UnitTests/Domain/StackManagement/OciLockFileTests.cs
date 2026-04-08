using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Domain.StackManagement;

public class OciLockFileTests
{
    [Fact]
    public void Create_ValidParams_CreatesLockFile()
    {
        var images = new[]
        {
            OciLockImage.Create("web", "nginx", "1.25-alpine", "sha256:abc123"),
            OciLockImage.Create("redis", "redis", "7-alpine", "sha256:def456")
        };

        var lockFile = OciLockFile.Create("1", "my-stack", "1.0.0", images);

        lockFile.ApiVersion.Should().Be("1");
        lockFile.StackName.Should().Be("my-stack");
        lockFile.StackVersion.Should().Be("1.0.0");
        lockFile.Images.Should().HaveCount(2);
    }

    [Fact]
    public void Create_EmptyStackName_Throws()
    {
        var act = () => OciLockFile.Create("1", "", "1.0.0", Array.Empty<OciLockImage>());
        act.Should().Throw<ArgumentException>().WithMessage("*Stack name*");
    }

    [Fact]
    public void Create_EmptyStackVersion_Throws()
    {
        var act = () => OciLockFile.Create("1", "stack", "", Array.Empty<OciLockImage>());
        act.Should().Throw<ArgumentException>().WithMessage("*Stack version*");
    }

    [Fact]
    public void Create_NullApiVersion_DefaultsToOne()
    {
        var lockFile = OciLockFile.Create(null!, "stack", "1.0.0", Array.Empty<OciLockImage>());
        lockFile.ApiVersion.Should().Be("1");
    }

    [Fact]
    public void ResolveDigest_ExistingService_ReturnsDigest()
    {
        var lockFile = OciLockFile.Create("1", "stack", "1.0.0", new[]
        {
            OciLockImage.Create("web", "nginx", "1.25", "sha256:abc123")
        });

        lockFile.ResolveDigest("web").Should().Be("sha256:abc123");
    }

    [Fact]
    public void ResolveDigest_NonExistingService_ReturnsNull()
    {
        var lockFile = OciLockFile.Create("1", "stack", "1.0.0", new[]
        {
            OciLockImage.Create("web", "nginx", "1.25", "sha256:abc123")
        });

        lockFile.ResolveDigest("redis").Should().BeNull();
    }

    [Fact]
    public void ResolveDigest_CaseInsensitive()
    {
        var lockFile = OciLockFile.Create("1", "stack", "1.0.0", new[]
        {
            OciLockImage.Create("Web", "nginx", "1.25", "sha256:abc123")
        });

        lockFile.ResolveDigest("web").Should().Be("sha256:abc123");
        lockFile.ResolveDigest("WEB").Should().Be("sha256:abc123");
    }

    [Fact]
    public void ResolveImageReference_WithDigest_ReturnsDigestRef()
    {
        var lockFile = OciLockFile.Create("1", "stack", "1.0.0", new[]
        {
            OciLockImage.Create("web", "nginx", "1.25-alpine", "sha256:abc123")
        });

        var result = lockFile.ResolveImageReference("web", "nginx:1.25-alpine");

        result.Should().Be("nginx@sha256:abc123");
    }

    [Fact]
    public void ResolveImageReference_WithoutDigest_ReturnsOriginal()
    {
        var lockFile = OciLockFile.Create("1", "stack", "1.0.0", Array.Empty<OciLockImage>());

        var result = lockFile.ResolveImageReference("unknown", "nginx:1.25-alpine");

        result.Should().Be("nginx:1.25-alpine");
    }

    [Fact]
    public void ResolveImageReference_ImageWithoutTag_ReturnsDigestRef()
    {
        var lockFile = OciLockFile.Create("1", "stack", "1.0.0", new[]
        {
            OciLockImage.Create("web", "nginx", "latest", "sha256:abc123")
        });

        var result = lockFile.ResolveImageReference("web", "nginx");

        result.Should().Be("nginx@sha256:abc123");
    }

    [Fact]
    public void ResolveImageReference_FullRegistryPath_PreservesPath()
    {
        var lockFile = OciLockFile.Create("1", "stack", "1.0.0", new[]
        {
            OciLockImage.Create("app", "ghcr.io/org/app", "v1.0", "sha256:def456")
        });

        var result = lockFile.ResolveImageReference("app", "ghcr.io/org/app:v1.0");

        result.Should().Be("ghcr.io/org/app@sha256:def456");
    }
}

public class OciLockImageTests
{
    [Fact]
    public void Create_ValidParams_CreatesImage()
    {
        var image = OciLockImage.Create("web", "nginx", "1.25", "sha256:abc123", "main");

        image.Name.Should().Be("web");
        image.Image.Should().Be("nginx");
        image.Tag.Should().Be("1.25");
        image.Digest.Should().Be("sha256:abc123");
        image.Role.Should().Be("main");
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var act = () => OciLockImage.Create("", "nginx", "1.25", "sha256:abc");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyDigest_Throws()
    {
        var act = () => OciLockImage.Create("web", "nginx", "1.25", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NullImage_DefaultsToEmpty()
    {
        var image = OciLockImage.Create("web", null!, null!, "sha256:abc");
        image.Image.Should().BeEmpty();
        image.Tag.Should().BeEmpty();
    }
}
