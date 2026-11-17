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
            entry.Category.Should().NotBeNullOrWhiteSpace();
            entry.Tags.Should().NotBeNull();

            if (entry.Type == "git-repository")
            {
                entry.GitUrl.Should().NotBeNullOrWhiteSpace($"git-repository entry '{entry.Id}' must have a GitUrl");
                entry.GitBranch.Should().NotBeNullOrWhiteSpace($"git-repository entry '{entry.Id}' must have a GitBranch");
            }
            else if (entry.Type == "local-directory")
            {
                entry.Path.Should().NotBeNullOrWhiteSpace($"local-directory entry '{entry.Id}' must have a Path");
            }
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
    public void GetAll_DoesNotContainRetiredAmsProjectChannels()
    {
        // The legacy stable channel (private repo with an outdated CI state)
        // and the RC channel stay retired. The current stable channel uses
        // its own id, rsgo-ams-project-stable.
        var service = CreateService();

        var entries = service.GetAll();

        entries.Should().NotContain(e => e.Id == "rsgo-ams-project-stacks");
        entries.Should().NotContain(e => e.Id == "rsgo-ams-project-rc");
    }

    [Theory]
    [InlineData("rsgo-ams-project")]
    [InlineData("rsgo-ams-project-stacks")]
    [InlineData("rsgo-ams-project-rc")]
    public void GetAll_NoEntryPointsToRetiredAmsProjectRepository(string retiredRepositoryName)
    {
        // Compare the repository name exactly: a substring check would also
        // match the current rsgo-ams-project-stable / -preview repositories.
        var service = CreateService();

        var repositoryNames = service.GetAll()
            .Where(e => e.Type == "git-repository")
            .Select(e => RepositoryName(e.GitUrl))
            .ToList();

        repositoryNames.Should().NotContain(retiredRepositoryName);
    }

    [Fact]
    public void GetAll_ContainsAmsProjectStableEntry()
    {
        var service = CreateService();

        var entries = service.GetAll();

        entries.Should().ContainSingle(e => e.Id == "rsgo-ams-project-stable");
    }

    [Fact]
    public void GetAll_AmsProjectStableHasCorrectData()
    {
        var service = CreateService();

        var entry = service.GetAll().First(e => e.Id == "rsgo-ams-project-stable");

        entry.Name.Should().Be("ams.project Stacks (Stable)");
        entry.Description.Should().NotBeNullOrWhiteSpace();
        entry.Type.Should().Be("git-repository");
        entry.GitUrl.Should().Be("https://github.com/Wiesenwischer/rsgo-ams-project-stable.git");
        entry.GitBranch.Should().Be("main");
        entry.Category.Should().Be("vendor");
        entry.Featured.Should().BeFalse("vendor sources must not be pre-selected for every installation");
        entry.StackCount.Should().Be(2);
        entry.Tags.Should().Contain(["enterprise", "vendor", "ams", "stable"]);
    }

    [Fact]
    public void GetAll_AmsProjectStableIsNotTaggedAsPreRelease()
    {
        var service = CreateService();

        var entry = service.GetAll().First(e => e.Id == "rsgo-ams-project-stable");

        entry.Tags.Should().NotContain("preview");
        entry.Tags.Should().NotContain("pre-release");
        entry.Name.Should().NotContainEquivalentOf("preview");
        entry.Description.Should().NotContainEquivalentOf("preview");
    }

    [Fact]
    public void GetAll_AmsProjectStableAndPreviewUseDifferentRepositories()
    {
        var service = CreateService();
        var entries = service.GetAll();

        var stable = entries.First(e => e.Id == "rsgo-ams-project-stable");
        var preview = entries.First(e => e.Id == "rsgo-ams-project-preview");

        RepositoryName(stable.GitUrl).Should().Be("rsgo-ams-project-stable");
        RepositoryName(preview.GitUrl).Should().Be("rsgo-ams-project-preview");
        stable.Name.Should().NotBe(preview.Name, "the wizard must tell both channels apart");
    }

    [Fact]
    public void GetAll_ListsAmsProjectStableBeforePreview()
    {
        // The wizard shows the entries in registry order; the production
        // channel comes first.
        var service = CreateService();

        var ids = service.GetAll().Select(e => e.Id).ToList();

        ids.IndexOf("rsgo-ams-project-stable").Should().BeLessThan(ids.IndexOf("rsgo-ams-project-preview"));
    }

    [Fact]
    public void GetAll_ContainsAmsProjectPreviewEntry()
    {
        var service = CreateService();

        var entries = service.GetAll();

        entries.Should().Contain(e => e.Id == "rsgo-ams-project-preview");
    }

    [Fact]
    public void GetAll_AmsProjectPreviewHasCorrectData()
    {
        var service = CreateService();

        var entry = service.GetAll().First(e => e.Id == "rsgo-ams-project-preview");

        entry.Name.Should().Be("ams.project Stacks (Preview)");
        entry.GitUrl.Should().Contain("github.com/Wiesenwischer/rsgo-ams-project-preview");
        entry.GitBranch.Should().Be("main");
        entry.Category.Should().Be("vendor");
        entry.Featured.Should().BeFalse();
        entry.Tags.Should().Contain("vendor");
        entry.Tags.Should().Contain("ams");
    }

    [Fact]
    public void GetAll_AllGitUrlsAreValidUris()
    {
        var service = CreateService();

        var entries = service.GetAll().Where(e => e.Type == "git-repository");

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

    [Fact]
    public void GetById_AmsProjectStable_ReturnsStableRepository()
    {
        // The wizard submits the selected ids; the stable id must resolve to
        // the stable repository, not to the preview one.
        var service = CreateService();

        var entry = service.GetById("rsgo-ams-project-stable");

        entry.Should().NotBeNull();
        entry!.GitUrl.Should().Be("https://github.com/Wiesenwischer/rsgo-ams-project-stable.git");
        entry.GitBranch.Should().Be("main");
    }

    [Fact]
    public void GetById_RetiredAmsProjectStableId_ReturnsNull()
    {
        var service = CreateService();

        var entry = service.GetById("rsgo-ams-project-stacks");

        entry.Should().BeNull("the legacy stable channel points to an outdated private repository");
    }

    #endregion

    private static string RepositoryName(string gitUrl)
    {
        var lastSegment = new Uri(gitUrl).AbsolutePath.TrimEnd('/').Split('/').Last();
        return lastSegment.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? lastSegment[..^4]
            : lastSegment;
    }
}
