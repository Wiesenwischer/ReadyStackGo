using FluentAssertions;
using ReadyStackGo.Infrastructure.Services.StackSources;

namespace ReadyStackGo.UnitTests.Infrastructure.StackSources;

public class OciTagFilterTests
{
    [Fact]
    public void FilterTagsByGlob_Wildcard_ReturnsAll()
    {
        var tags = new[] { "v1.0.0", "v2.0.0", "latest", "dev" };
        var result = OciRegistryProductSourceProvider.FilterTagsByGlob(tags, "*");
        result.Should().HaveCount(4);
    }

    [Fact]
    public void FilterTagsByGlob_PrefixPattern_FiltersCorrectly()
    {
        var tags = new[] { "v1.0.0", "v2.0.0", "latest", "dev", "v1.1.0" };
        var result = OciRegistryProductSourceProvider.FilterTagsByGlob(tags, "v*");
        result.Should().HaveCount(3);
        result.Should().Contain("v1.0.0");
        result.Should().Contain("v2.0.0");
        result.Should().Contain("v1.1.0");
    }

    [Fact]
    public void FilterTagsByGlob_ComplexPattern_FiltersCorrectly()
    {
        var tags = new[] { "ams-1.0.0", "ams-2.0.0", "web-1.0.0", "ams-dev" };
        var result = OciRegistryProductSourceProvider.FilterTagsByGlob(tags, "ams-*");
        result.Should().HaveCount(3);
        result.Should().NotContain("web-1.0.0");
    }

    [Fact]
    public void FilterTagsByGlob_QuestionMark_MatchesSingleChar()
    {
        var tags = new[] { "v1", "v2", "v10", "v20" };
        var result = OciRegistryProductSourceProvider.FilterTagsByGlob(tags, "v?");
        result.Should().HaveCount(2);
        result.Should().Contain("v1");
        result.Should().Contain("v2");
    }

    [Fact]
    public void FilterTagsByGlob_CaseInsensitive()
    {
        var tags = new[] { "V1.0.0", "v2.0.0", "LATEST" };
        var result = OciRegistryProductSourceProvider.FilterTagsByGlob(tags, "v*");
        result.Should().HaveCount(2);
    }

    [Fact]
    public void FilterTagsByGlob_NoMatches_ReturnsEmpty()
    {
        var tags = new[] { "latest", "dev" };
        var result = OciRegistryProductSourceProvider.FilterTagsByGlob(tags, "v*");
        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterTagsByGlob_EmptyTags_ReturnsEmpty()
    {
        var result = OciRegistryProductSourceProvider.FilterTagsByGlob(Array.Empty<string>(), "v*");
        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterTagsByGlob_ExactMatch_ReturnsOne()
    {
        var tags = new[] { "latest", "dev", "stable" };
        var result = OciRegistryProductSourceProvider.FilterTagsByGlob(tags, "latest");
        result.Should().HaveCount(1);
        result.Should().Contain("latest");
    }
}
