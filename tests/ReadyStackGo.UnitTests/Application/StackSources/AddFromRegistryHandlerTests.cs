using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.StackSources.AddFromRegistry;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.UnitTests.Application.StackSources;

public class AddFromRegistryHandlerTests
{
    private readonly Mock<ISourceRegistryService> _registryMock = new();
    private readonly Mock<IProductSourceService> _productSourceMock = new();
    private readonly Mock<ILogger<AddFromRegistryHandler>> _loggerMock = new();

    private AddFromRegistryHandler CreateHandler()
        => new(_registryMock.Object, _productSourceMock.Object, _loggerMock.Object);

    private static SourceRegistryEntry CreateRegistryEntry(
        string id = "test-source",
        string name = "Test Source",
        string gitUrl = "https://github.com/test/repo.git",
        string gitBranch = "main")
    {
        return new SourceRegistryEntry(
            Id: id,
            Name: name,
            Description: "A test source",
            GitUrl: gitUrl,
            GitBranch: gitBranch,
            Category: "community",
            Tags: ["test"],
            Featured: false,
            StackCount: 5);
    }

    private void SetupExistingSources(params StackSource[] sources)
    {
        _productSourceMock
            .Setup(s => s.GetSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);

        _productSourceMock
            .Setup(s => s.AddSourceAsync(It.IsAny<StackSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StackSource src, CancellationToken _) => src);
    }

    #region Successful Creation

    [Fact]
    public async Task Handle_ValidRegistryId_ReturnsSuccess()
    {
        var entry = CreateRegistryEntry();
        _registryMock.Setup(r => r.GetById("test-source")).Returns(entry);
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddFromRegistryCommand("test-source"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ValidRegistryId_CreatesGitSource()
    {
        var entry = CreateRegistryEntry(name: "Community Stacks", gitUrl: "https://github.com/test/stacks.git", gitBranch: "develop");
        _registryMock.Setup(r => r.GetById("test-source")).Returns(entry);
        SetupExistingSources();
        var handler = CreateHandler();

        await handler.Handle(new AddFromRegistryCommand("test-source"), CancellationToken.None);

        _productSourceMock.Verify(
            s => s.AddSourceAsync(
                It.Is<StackSource>(src =>
                    src.Name == "Community Stacks" &&
                    src.GitUrl == "https://github.com/test/stacks.git" &&
                    src.GitBranch == "develop" &&
                    src.Type == StackSourceType.GitRepository),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Registry Entry Not Found

    [Fact]
    public async Task Handle_UnknownId_ReturnsFailure()
    {
        _registryMock.Setup(r => r.GetById("unknown")).Returns((SourceRegistryEntry?)null);
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddFromRegistryCommand("unknown"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_UnknownId_DoesNotCreateSource()
    {
        _registryMock.Setup(r => r.GetById("unknown")).Returns((SourceRegistryEntry?)null);
        SetupExistingSources();
        var handler = CreateHandler();

        await handler.Handle(new AddFromRegistryCommand("unknown"), CancellationToken.None);

        _productSourceMock.Verify(
            s => s.AddSourceAsync(It.IsAny<StackSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Duplicate Detection

    [Fact]
    public async Task Handle_SourceAlreadyExists_ReturnsFailure()
    {
        var entry = CreateRegistryEntry(gitUrl: "https://github.com/test/repo.git");
        _registryMock.Setup(r => r.GetById("test-source")).Returns(entry);
        var existing = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Existing", "https://github.com/test/repo.git");
        SetupExistingSources(existing);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddFromRegistryCommand("test-source"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task Handle_DuplicateMatchingIsCaseInsensitive()
    {
        var entry = CreateRegistryEntry(gitUrl: "https://github.com/Test/Repo.git");
        _registryMock.Setup(r => r.GetById("test-source")).Returns(entry);
        var existing = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Existing", "https://github.com/test/repo.git");
        SetupExistingSources(existing);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddFromRegistryCommand("test-source"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DuplicateMatchingIgnoresTrailingSlash()
    {
        var entry = CreateRegistryEntry(gitUrl: "https://github.com/test/repo.git/");
        _registryMock.Setup(r => r.GetById("test-source")).Returns(entry);
        var existing = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Existing", "https://github.com/test/repo.git");
        SetupExistingSources(existing);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddFromRegistryCommand("test-source"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LocalSourceDoesNotBlockGitSource()
    {
        var entry = CreateRegistryEntry(gitUrl: "https://github.com/test/repo.git");
        _registryMock.Setup(r => r.GetById("test-source")).Returns(entry);
        var localSource = StackSource.CreateLocalDirectory(
            StackSourceId.NewId(), "Local", "/stacks");
        SetupExistingSources(localSource);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddFromRegistryCommand("test-source"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    #endregion
}
