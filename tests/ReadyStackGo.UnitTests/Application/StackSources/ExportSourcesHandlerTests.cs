using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.StackSources.ExportSources;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.UnitTests.Application.StackSources;

public class ExportSourcesHandlerTests
{
    private readonly Mock<IProductSourceService> _productSourceMock = new();

    private ExportSourcesHandler CreateHandler()
        => new(_productSourceMock.Object);

    private void SetupSources(params StackSource[] sources)
    {
        _productSourceMock
            .Setup(s => s.GetSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);
    }

    #region Empty Sources

    [Fact]
    public async Task Handle_NoSources_ReturnsEmptyExport()
    {
        SetupSources();
        var handler = CreateHandler();

        var result = await handler.Handle(new ExportSourcesQuery(), CancellationToken.None);

        result.Data.Sources.Should().BeEmpty();
        result.Data.Version.Should().Be("1.0");
    }

    #endregion

    #region Git Repository Export

    [Fact]
    public async Task Handle_GitSource_ExportsCorrectFields()
    {
        var source = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "My Git", "https://github.com/test/repo.git", "develop", sslVerify: false);
        SetupSources(source);
        var handler = CreateHandler();

        var result = await handler.Handle(new ExportSourcesQuery(), CancellationToken.None);

        var exported = result.Data.Sources.Should().ContainSingle().Subject;
        exported.Name.Should().Be("My Git");
        exported.Type.Should().Be("git-repository");
        exported.GitUrl.Should().Be("https://github.com/test/repo.git");
        exported.GitBranch.Should().Be("develop");
        exported.GitSslVerify.Should().BeFalse();
        exported.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DisabledGitSource_ExportsEnabledFalse()
    {
        var source = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Disabled Git", "https://github.com/test/repo.git");
        source.Disable();
        SetupSources(source);
        var handler = CreateHandler();

        var result = await handler.Handle(new ExportSourcesQuery(), CancellationToken.None);

        result.Data.Sources.Should().ContainSingle()
            .Which.Enabled.Should().BeFalse();
    }

    #endregion

    #region Local Directory Export

    [Fact]
    public async Task Handle_LocalSource_ExportsCorrectFields()
    {
        var source = StackSource.CreateLocalDirectory(
            StackSourceId.NewId(), "My Local", "/stacks", "*.yml");
        SetupSources(source);
        var handler = CreateHandler();

        var result = await handler.Handle(new ExportSourcesQuery(), CancellationToken.None);

        var exported = result.Data.Sources.Should().ContainSingle().Subject;
        exported.Name.Should().Be("My Local");
        exported.Type.Should().Be("local-directory");
        exported.Path.Should().Be("/stacks");
        exported.FilePattern.Should().Be("*.yml");
        exported.GitSslVerify.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LocalSource_DoesNotIncludeGitFields()
    {
        var source = StackSource.CreateLocalDirectory(
            StackSourceId.NewId(), "Local Only", "/data");
        SetupSources(source);
        var handler = CreateHandler();

        var result = await handler.Handle(new ExportSourcesQuery(), CancellationToken.None);

        var exported = result.Data.Sources.Should().ContainSingle().Subject;
        exported.GitUrl.Should().BeNull();
        exported.GitBranch.Should().BeNull();
        exported.GitSslVerify.Should().BeNull();
    }

    #endregion

    #region Multiple Sources

    [Fact]
    public async Task Handle_MultipleSources_ExportsAll()
    {
        var git = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Git One", "https://github.com/test/a.git");
        var local = StackSource.CreateLocalDirectory(
            StackSourceId.NewId(), "Local One", "/stacks");
        var git2 = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Git Two", "https://github.com/test/b.git");
        SetupSources(git, local, git2);
        var handler = CreateHandler();

        var result = await handler.Handle(new ExportSourcesQuery(), CancellationToken.None);

        result.Data.Sources.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ExportedAtIsRecentUtc()
    {
        SetupSources();
        var handler = CreateHandler();
        var before = DateTime.UtcNow;

        var result = await handler.Handle(new ExportSourcesQuery(), CancellationToken.None);

        result.Data.ExportedAt.Should().BeOnOrAfter(before);
        result.Data.ExportedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    #endregion

    #region Git SSL Verify Default

    [Fact]
    public async Task Handle_GitSourceDefaultSslVerify_ExportsTrue()
    {
        var source = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Default SSL", "https://github.com/test/repo.git");
        SetupSources(source);
        var handler = CreateHandler();

        var result = await handler.Handle(new ExportSourcesQuery(), CancellationToken.None);

        result.Data.Sources.Should().ContainSingle()
            .Which.GitSslVerify.Should().BeTrue();
    }

    #endregion
}
