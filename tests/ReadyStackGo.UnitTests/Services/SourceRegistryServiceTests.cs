using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Infrastructure.Services.StackSources;

namespace ReadyStackGo.UnitTests.Services;

public class SourceRegistryServiceTests
{
    private readonly Mock<ILogger<SourceRegistryService>> _loggerMock = new();

    private SourceRegistryService CreateService()
        => new(_loggerMock.Object);

    #region GetAll Tests

    [Fact]
    public void GetAll_ReturnsNonEmptyList()
    {
        var service = CreateService();

        var entries = service.GetAll();

        entries.Should().NotBeEmpty();
    }

    [Fact]
    public void GetAll_AllEntriesHaveRequiredFields()
    {
        var service = CreateService();

        var entries = service.GetAll();

        foreach (var entry in entries)
        {
            entry.Id.Should().NotBeNullOrWhiteSpace();
            entry.Name.Should().NotBeNullOrWhiteSpace();
            entry.Description.Should().NotBeNullOrWhiteSpace();
            entry.GitUrl.Should().NotBeNullOrWhiteSpace();
            entry.GitBranch.Should().NotBeNullOrWhiteSpace();
            entry.Category.Should().NotBeNullOrWhiteSpace();
            entry.Tags.Should().NotBeNull();
        }
    }

    [Fact]
    public void GetAll_ReturnsSameInstanceOnMultipleCalls()
    {
        var service = CreateService();

        var first = service.GetAll();
        var second = service.GetAll();

        first.Should().BeSameAs(second, "registry is loaded lazily and cached");
    }

    [Fact]
    public void GetAll_ContainsCommunityStacksEntry()
    {
        var service = CreateService();

        var entries = service.GetAll();

        entries.Should().Contain(e => e.Id == "rsgo-community-stacks");
    }

    [Fact]
    public void GetAll_CommunityStacksHasCorrectData()
    {
        var service = CreateService();

        var entry = service.GetAll().First(e => e.Id == "rsgo-community-stacks");

        entry.Name.Should().Be("RSGO Community Stacks");
        entry.GitUrl.Should().Contain("github.com/Wiesenwischer/rsgo-community-stacks");
        entry.GitBranch.Should().Be("main");
        entry.Category.Should().Be("official");
        entry.Featured.Should().BeTrue();
        entry.Tags.Should().Contain("official");
    }

    [Fact]
    public void GetAll_AllGitUrlsAreValidUris()
    {
        var service = CreateService();

        var entries = service.GetAll();

        foreach (var entry in entries)
        {
            var isValidUri = Uri.TryCreate(entry.GitUrl, UriKind.Absolute, out var uri);
            isValidUri.Should().BeTrue($"GitUrl '{entry.GitUrl}' for entry '{entry.Id}' should be a valid URI");
            uri!.Scheme.Should().BeOneOf("https", "http", "git");
        }
    }

    [Fact]
    public void GetAll_AllIdsAreUnique()
    {
        var service = CreateService();

        var entries = service.GetAll();
        var ids = entries.Select(e => e.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region GetById Tests

    [Fact]
    public void GetById_ExistingId_ReturnsEntry()
    {
        var service = CreateService();

        var entry = service.GetById("rsgo-community-stacks");

        entry.Should().NotBeNull();
        entry!.Id.Should().Be("rsgo-community-stacks");
    }

    [Fact]
    public void GetById_NonExistingId_ReturnsNull()
    {
        var service = CreateService();

        var entry = service.GetById("non-existing-id");

        entry.Should().BeNull();
    }

    [Fact]
    public void GetById_EmptyString_ReturnsNull()
    {
        var service = CreateService();

        var entry = service.GetById("");

        entry.Should().BeNull();
    }

    [Fact]
    public void GetById_IsCaseSensitive()
    {
        var service = CreateService();

        var entry = service.GetById("RSGO-COMMUNITY-STACKS");

        entry.Should().BeNull("ID lookup should be case-sensitive");
    }

    #endregion
}
