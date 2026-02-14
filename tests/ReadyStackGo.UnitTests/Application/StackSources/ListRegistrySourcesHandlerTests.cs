using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.StackSources.ListRegistrySources;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.UnitTests.Application.StackSources;

public class ListRegistrySourcesHandlerTests
{
    private readonly Mock<ISourceRegistryService> _registryMock = new();
    private readonly Mock<IProductSourceService> _productSourceMock = new();

    private ListRegistrySourcesHandler CreateHandler()
        => new(_registryMock.Object, _productSourceMock.Object);

    private static SourceRegistryEntry CreateRegistryEntry(
        string id = "test-source",
        string name = "Test Source",
        string gitUrl = "https://github.com/test/repo.git",
        bool featured = false)
    {
        return new SourceRegistryEntry(
            Id: id,
            Name: name,
            Description: "A test source",
            GitUrl: gitUrl,
            GitBranch: "main",
            Category: "community",
            Tags: ["test"],
            Featured: featured,
            StackCount: 5);
    }

    private static StackSource CreateGitSource(string gitUrl)
    {
        return StackSource.CreateGitRepository(
            StackSourceId.NewId(),
            "Existing Source",
            gitUrl);
    }

    private void SetupRegistry(params SourceRegistryEntry[] entries)
    {
        _registryMock.Setup(r => r.GetAll()).Returns(entries.ToList().AsReadOnly());
    }

    private void SetupExistingSources(params StackSource[] sources)
    {
        _productSourceMock
            .Setup(s => s.GetSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);
    }

    #region Basic Behavior

    [Fact]
    public async Task Handle_EmptyRegistry_ReturnsEmptyList()
    {
        SetupRegistry();
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RegistryWithEntries_ReturnsAllEntries()
    {
        var entry1 = CreateRegistryEntry(id: "source-1", gitUrl: "https://github.com/test/one.git");
        var entry2 = CreateRegistryEntry(id: "source-2", gitUrl: "https://github.com/test/two.git");
        SetupRegistry(entry1, entry2);
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Should().HaveCount(2);
        result.Sources.Select(s => s.Id).Should().Contain(["source-1", "source-2"]);
    }

    [Fact]
    public async Task Handle_MapsAllFieldsCorrectly()
    {
        var entry = new SourceRegistryEntry(
            Id: "mapped-source",
            Name: "Mapped Source",
            Description: "Description for mapping test",
            GitUrl: "https://github.com/test/mapped.git",
            GitBranch: "develop",
            Category: "official",
            Tags: ["tag1", "tag2"],
            Featured: true,
            StackCount: 42);
        SetupRegistry(entry);
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        var item = result.Sources.Single();
        item.Id.Should().Be("mapped-source");
        item.Name.Should().Be("Mapped Source");
        item.Description.Should().Be("Description for mapping test");
        item.GitUrl.Should().Be("https://github.com/test/mapped.git");
        item.GitBranch.Should().Be("develop");
        item.Category.Should().Be("official");
        item.Tags.Should().BeEquivalentTo(["tag1", "tag2"]);
        item.Featured.Should().BeTrue();
        item.StackCount.Should().Be(42);
    }

    #endregion

    #region AlreadyAdded Detection

    [Fact]
    public async Task Handle_SourceNotAdded_AlreadyAddedIsFalse()
    {
        SetupRegistry(CreateRegistryEntry(gitUrl: "https://github.com/test/repo.git"));
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SourceAlreadyAdded_AlreadyAddedIsTrue()
    {
        SetupRegistry(CreateRegistryEntry(gitUrl: "https://github.com/test/repo.git"));
        SetupExistingSources(CreateGitSource("https://github.com/test/repo.git"));
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UrlMatchingIsCaseInsensitive()
    {
        SetupRegistry(CreateRegistryEntry(gitUrl: "https://github.com/Test/Repo.git"));
        SetupExistingSources(CreateGitSource("https://github.com/test/repo.git"));
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UrlMatchingIgnoresTrailingSlash()
    {
        SetupRegistry(CreateRegistryEntry(gitUrl: "https://github.com/test/repo.git/"));
        SetupExistingSources(CreateGitSource("https://github.com/test/repo.git"));
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MixedAddedAndNotAdded()
    {
        var added = CreateRegistryEntry(id: "added", gitUrl: "https://github.com/test/added.git");
        var notAdded = CreateRegistryEntry(id: "not-added", gitUrl: "https://github.com/test/new.git");
        SetupRegistry(added, notAdded);
        SetupExistingSources(CreateGitSource("https://github.com/test/added.git"));
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.First(s => s.Id == "added").AlreadyAdded.Should().BeTrue();
        result.Sources.First(s => s.Id == "not-added").AlreadyAdded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LocalDirectorySourcesDoNotAffectGitMatching()
    {
        SetupRegistry(CreateRegistryEntry(gitUrl: "https://github.com/test/repo.git"));
        var localSource = StackSource.CreateLocalDirectory(
            StackSourceId.NewId(), "Local", "/stacks");
        SetupExistingSources(localSource);
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeFalse();
    }

    #endregion

    #region Local Directory Registry Entries

    [Fact]
    public async Task Handle_LocalRegistryEntry_NotAdded_AlreadyAddedIsFalse()
    {
        SetupRegistry(CreateLocalRegistryEntry());
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LocalRegistryEntry_AlreadyAdded_AlreadyAddedIsTrue()
    {
        SetupRegistry(CreateLocalRegistryEntry(path: "stacks"));
        var existing = StackSource.CreateLocalDirectory(
            StackSourceId.NewId(), "Local", "stacks");
        SetupExistingSources(existing);
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LocalRegistryEntryPathMatchingIsCaseInsensitive()
    {
        SetupRegistry(CreateLocalRegistryEntry(path: "Stacks"));
        var existing = StackSource.CreateLocalDirectory(
            StackSourceId.NewId(), "Local", "stacks");
        SetupExistingSources(existing);
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MapsLocalRegistryEntryTypeCorrectly()
    {
        SetupRegistry(CreateLocalRegistryEntry());
        SetupExistingSources();
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        var item = result.Sources.Single();
        item.Type.Should().Be("local-directory");
        item.Path.Should().Be("stacks");
        item.FilePattern.Should().Be("*.yml;*.yaml");
    }

    [Fact]
    public async Task Handle_GitSourceDoesNotAffectLocalMatching()
    {
        SetupRegistry(CreateLocalRegistryEntry(path: "stacks"));
        var gitSource = StackSource.CreateGitRepository(
            StackSourceId.NewId(), "Git", "https://github.com/test/repo.git");
        SetupExistingSources(gitSource);
        var handler = CreateHandler();

        var result = await handler.Handle(new ListRegistrySourcesQuery(), CancellationToken.None);

        result.Sources.Single().AlreadyAdded.Should().BeFalse();
    }

    #endregion

    private static SourceRegistryEntry CreateLocalRegistryEntry(
        string id = "local-stacks",
        string name = "Local Stacks",
        string path = "stacks")
    {
        return new SourceRegistryEntry(
            Id: id,
            Name: name,
            Description: "Built-in stacks",
            GitUrl: "",
            GitBranch: "",
            Category: "built-in",
            Tags: ["embedded"],
            Featured: true,
            StackCount: 3,
            Type: "local-directory",
            Path: path,
            FilePattern: "*.yml;*.yaml");
    }
}
