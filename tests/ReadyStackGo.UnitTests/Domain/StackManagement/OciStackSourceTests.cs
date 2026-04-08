using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.UnitTests.Domain.StackManagement;

public class OciStackSourceTests
{
    [Fact]
    public void CreateOciRegistry_ValidParams_CreatesSource()
    {
        var id = new StackSourceId("test-oci");
        var source = StackSource.CreateOciRegistry(id, "My OCI", "ghcr.io", "org/stacks");

        source.Id.Should().Be(id);
        source.Name.Should().Be("My OCI");
        source.Type.Should().Be(StackSourceType.OciRegistry);
        source.RegistryUrl.Should().Be("ghcr.io");
        source.Repository.Should().Be("org/stacks");
        source.TagPattern.Should().Be("*");
        source.Enabled.Should().BeTrue();
    }

    [Fact]
    public void CreateOciRegistry_WithCredentials_StoresEncrypted()
    {
        var source = StackSource.CreateOciRegistry(
            new StackSourceId("test"), "Test", "docker.io", "myorg/stacks",
            tagPattern: "v*", username: "user", password: "secret");

        source.RegistryUsername.Should().Be("user");
        source.RegistryPassword.Should().Be("secret");
        source.TagPattern.Should().Be("v*");
    }

    [Fact]
    public void CreateOciRegistry_EmptyRegistryUrl_Throws()
    {
        var act = () => StackSource.CreateOciRegistry(
            new StackSourceId("test"), "Test", "", "repo");

        act.Should().Throw<ArgumentException>().WithMessage("*Registry URL*");
    }

    [Fact]
    public void CreateOciRegistry_EmptyRepository_Throws()
    {
        var act = () => StackSource.CreateOciRegistry(
            new StackSourceId("test"), "Test", "ghcr.io", "");

        act.Should().Throw<ArgumentException>().WithMessage("*Repository*");
    }

    [Fact]
    public void CreateOciRegistry_NullTagPattern_DefaultsToWildcard()
    {
        var source = StackSource.CreateOciRegistry(
            new StackSourceId("test"), "Test", "ghcr.io", "repo", tagPattern: null);

        source.TagPattern.Should().Be("*");
    }

    [Fact]
    public void UpdateRegistryCredentials_OciSource_Updates()
    {
        var source = StackSource.CreateOciRegistry(
            new StackSourceId("test"), "Test", "ghcr.io", "repo");

        source.UpdateRegistryCredentials("newuser", "newpass");

        source.RegistryUsername.Should().Be("newuser");
        source.RegistryPassword.Should().Be("newpass");
    }

    [Fact]
    public void UpdateRegistryCredentials_NonOciSource_Throws()
    {
        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("test"), "Test", "/stacks");

        var act = () => source.UpdateRegistryCredentials("user", "pass");

        act.Should().Throw<InvalidOperationException>();
    }
}
