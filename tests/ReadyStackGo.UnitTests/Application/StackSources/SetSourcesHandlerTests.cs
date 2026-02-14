using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Wizard.SetSources;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.UnitTests.Application.StackSources;

public class SetSourcesHandlerTests
{
    private readonly Mock<ISourceRegistryService> _registryMock = new();
    private readonly Mock<IProductSourceService> _productSourceMock = new();
    private readonly Mock<ILogger<SetSourcesHandler>> _loggerMock = new();

    private SetSourcesHandler CreateHandler()
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

    private void SetupRegistry(params SourceRegistryEntry[] entries)
    {
        foreach (var entry in entries)
        {
            _registryMock.Setup(r => r.GetById(entry.Id)).Returns(entry);
        }
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

    #region Empty / No Selection

    [Fact]
    public async Task Handle_EmptyList_ReturnsSuccessWithZeroCreated()
    {
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetSourcesCommand([]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourcesCreated.Should().Be(0);
    }

    #endregion

    #region Successful Creation

    [Fact]
    public async Task Handle_ValidRegistryId_CreatesSource()
    {
        var entry = CreateRegistryEntry();
        SetupRegistry(entry);
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetSourcesCommand(["test-source"]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourcesCreated.Should().Be(1);

        _productSourceMock.Verify(
            s => s.AddSourceAsync(
                It.Is<StackSource>(src =>
                    src.Name == "Test Source" &&
                    src.GitUrl == "https://github.com/test/repo.git" &&
                    src.GitBranch == "main"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleSources_CreatesAll()
    {
        var entry1 = CreateRegistryEntry(id: "source-1", gitUrl: "https://github.com/test/one.git");
        var entry2 = CreateRegistryEntry(id: "source-2", gitUrl: "https://github.com/test/two.git");
        SetupRegistry(entry1, entry2);
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetSourcesCommand(["source-1", "source-2"]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourcesCreated.Should().Be(2);
    }

    #endregion

    #region Duplicate / Already Added

    [Fact]
    public async Task Handle_SourceAlreadyAdded_SkipsIt()
    {
        var entry = CreateRegistryEntry(gitUrl: "https://github.com/test/repo.git");
        SetupRegistry(entry);
        var existing = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Existing", "https://github.com/test/repo.git");
        SetupExistingSources(existing);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetSourcesCommand(["test-source"]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourcesCreated.Should().Be(0);

        _productSourceMock.Verify(
            s => s.AddSourceAsync(It.IsAny<StackSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateUrlMatchingIsCaseInsensitive()
    {
        var entry = CreateRegistryEntry(gitUrl: "https://github.com/Test/Repo.git");
        SetupRegistry(entry);
        var existing = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Existing", "https://github.com/test/repo.git");
        SetupExistingSources(existing);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetSourcesCommand(["test-source"]),
            CancellationToken.None);

        result.SourcesCreated.Should().Be(0);
    }

    #endregion

    #region Unknown Registry ID

    [Fact]
    public async Task Handle_UnknownRegistryId_SkipsIt()
    {
        _registryMock.Setup(r => r.GetById("unknown")).Returns((SourceRegistryEntry?)null);
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetSourcesCommand(["unknown"]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourcesCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MixedKnownAndUnknown_CreatesOnlyKnown()
    {
        var entry = CreateRegistryEntry(id: "valid", gitUrl: "https://github.com/test/valid.git");
        SetupRegistry(entry);
        _registryMock.Setup(r => r.GetById("invalid")).Returns((SourceRegistryEntry?)null);
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetSourcesCommand(["valid", "invalid"]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SourcesCreated.Should().Be(1);
    }

    #endregion

    #region Branch Propagation

    [Fact]
    public async Task Handle_RegistryBranch_PropagatedToSource()
    {
        var entry = CreateRegistryEntry(gitBranch: "develop");
        SetupRegistry(entry);
        SetupExistingSources();
        var handler = CreateHandler();

        await handler.Handle(
            new SetSourcesCommand(["test-source"]),
            CancellationToken.None);

        _productSourceMock.Verify(
            s => s.AddSourceAsync(
                It.Is<StackSource>(src => src.GitBranch == "develop"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
