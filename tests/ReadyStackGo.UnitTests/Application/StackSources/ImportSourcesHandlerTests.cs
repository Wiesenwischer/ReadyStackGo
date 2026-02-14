using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.StackSources.ImportSources;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.UnitTests.Application.StackSources;

public class ImportSourcesHandlerTests
{
    private readonly Mock<IProductSourceService> _productSourceMock = new();
    private readonly Mock<ILogger<ImportSourcesHandler>> _loggerMock = new();

    private ImportSourcesHandler CreateHandler()
        => new(_productSourceMock.Object, _loggerMock.Object);

    private void SetupExistingSources(params StackSource[] sources)
    {
        _productSourceMock
            .Setup(s => s.GetSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);

        _productSourceMock
            .Setup(s => s.AddSourceAsync(It.IsAny<StackSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StackSource src, CancellationToken _) => src);
    }

    private static ImportData CreateImportData(params ImportedSource[] sources)
        => new("1.0", sources);

    #region Empty Import

    [Fact]
    public async Task Handle_EmptySources_ReturnsSuccessWithZeroCounts()
    {
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData()),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourcesCreated.Should().Be(0);
        result.SourcesSkipped.Should().Be(0);
        result.Message.Should().Contain("No sources to import");
    }

    #endregion

    #region Git Repository Import

    [Fact]
    public async Task Handle_ValidGitSource_CreatesSource()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("My Repo", "git-repository", true,
            null, null, "https://github.com/test/repo.git", "main", true);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourcesCreated.Should().Be(1);
        _productSourceMock.Verify(
            s => s.AddSourceAsync(
                It.Is<StackSource>(src =>
                    src.Name == "My Repo" &&
                    src.GitUrl == "https://github.com/test/repo.git" &&
                    src.GitBranch == "main" &&
                    src.Type == StackSourceType.GitRepository),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GitSourceMissingUrl_SkipsSource()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("No URL", "git-repository", true,
            null, null, null, "main", null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesCreated.Should().Be(0);
        result.SourcesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_GitSourceEmptyUrl_SkipsSource()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("Empty URL", "git-repository", true,
            null, null, "  ", "main", null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_GitSourceNoBranch_DefaultsToMain()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("No Branch", "git-repository", true,
            null, null, "https://github.com/test/repo.git", null, null);

        await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        _productSourceMock.Verify(
            s => s.AddSourceAsync(
                It.Is<StackSource>(src => src.GitBranch == "main"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GitSourceDisabled_CreatesDisabledSource()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("Disabled", "git-repository", false,
            null, null, "https://github.com/test/repo.git", "main", null);

        await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        _productSourceMock.Verify(
            s => s.AddSourceAsync(
                It.Is<StackSource>(src => !src.Enabled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlternateGitTypeName_AcceptsGitRepository()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("Alt Type", "GitRepository", true,
            null, null, "https://github.com/test/repo.git", "main", null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesCreated.Should().Be(1);
    }

    #endregion

    #region Local Directory Import

    [Fact]
    public async Task Handle_ValidLocalSource_CreatesSource()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("Local Stacks", "local-directory", true,
            "/opt/stacks", "*.yml", null, null, null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesCreated.Should().Be(1);
        _productSourceMock.Verify(
            s => s.AddSourceAsync(
                It.Is<StackSource>(src =>
                    src.Name == "Local Stacks" &&
                    src.Path == "/opt/stacks" &&
                    src.FilePattern == "*.yml" &&
                    src.Type == StackSourceType.LocalDirectory),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LocalSourceMissingPath_SkipsSource()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("No Path", "local-directory", true,
            null, "*.yml", null, null, null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LocalSourceNoFilePattern_DefaultsToYml()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("Default Pattern", "local-directory", true,
            "/stacks", null, null, null, null);

        await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        _productSourceMock.Verify(
            s => s.AddSourceAsync(
                It.Is<StackSource>(src => src.FilePattern == "*.yml;*.yaml"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlternateLocalTypeName_AcceptsLocalDirectory()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("Alt Local", "LocalDirectory", true,
            "/stacks", null, null, null, null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesCreated.Should().Be(1);
    }

    #endregion

    #region Duplicate Detection

    [Fact]
    public async Task Handle_DuplicateGitUrl_SkipsSource()
    {
        var existing = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Existing", "https://github.com/test/repo.git");
        SetupExistingSources(existing);
        var handler = CreateHandler();
        var source = new ImportedSource("Dupe", "git-repository", true,
            null, null, "https://github.com/test/repo.git", "main", null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesSkipped.Should().Be(1);
        result.SourcesCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DuplicateDetectionIsCaseInsensitive()
    {
        var existing = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Existing", "https://github.com/test/repo.git");
        SetupExistingSources(existing);
        var handler = CreateHandler();
        var source = new ImportedSource("Dupe", "git-repository", true,
            null, null, "https://GitHub.com/Test/Repo.git", "main", null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateDetectionIgnoresTrailingSlash()
    {
        var existing = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Existing", "https://github.com/test/repo.git");
        SetupExistingSources(existing);
        var handler = CreateHandler();
        var source = new ImportedSource("Dupe", "git-repository", true,
            null, null, "https://github.com/test/repo.git/", "main", null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateWithinImportBatch_SkipsSecond()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var first = new ImportedSource("First", "git-repository", true,
            null, null, "https://github.com/test/repo.git", "main", null);
        var second = new ImportedSource("Second", "git-repository", true,
            null, null, "https://github.com/test/repo.git", "develop", null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(first, second)),
            CancellationToken.None);

        result.SourcesCreated.Should().Be(1);
        result.SourcesSkipped.Should().Be(1);
    }

    #endregion

    #region Unknown Type

    [Fact]
    public async Task Handle_UnknownType_SkipsSource()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var source = new ImportedSource("Unknown", "ftp-server", true,
            null, null, "ftp://example.com", null, null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(source)),
            CancellationToken.None);

        result.SourcesSkipped.Should().Be(1);
        result.SourcesCreated.Should().Be(0);
    }

    #endregion

    #region Mixed Sources

    [Fact]
    public async Task Handle_MixedValidAndInvalid_ReportsCorrectCounts()
    {
        SetupExistingSources();
        var handler = CreateHandler();
        var valid1 = new ImportedSource("Git OK", "git-repository", true,
            null, null, "https://github.com/test/a.git", "main", null);
        var valid2 = new ImportedSource("Local OK", "local-directory", true,
            "/stacks", null, null, null, null);
        var noUrl = new ImportedSource("No URL", "git-repository", true,
            null, null, null, null, null);
        var unknown = new ImportedSource("Unknown", "s3-bucket", true,
            null, null, null, null, null);

        var result = await handler.Handle(
            new ImportSourcesCommand(CreateImportData(valid1, valid2, noUrl, unknown)),
            CancellationToken.None);

        result.SourcesCreated.Should().Be(2);
        result.SourcesSkipped.Should().Be(2);
        result.Success.Should().BeTrue();
    }

    #endregion
}
