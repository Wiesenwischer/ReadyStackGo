using FluentAssertions;
using ReadyStackGo.Domain.SharedKernel;
using Xunit;

namespace ReadyStackGo.UnitTests.Domain;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("4.0.1", "4.0.0")]      // patch
    [InlineData("4.1.0", "4.0.9")]      // minor beats higher patch
    [InlineData("5.0.0", "4.9.9")]      // major
    [InlineData("v2.0.0", "1.0.0")]     // leading v ignored
    public void Compare_NumericBase(string greater, string lesser)
    {
        SemanticVersion.Compare(greater, lesser).Should().BePositive();
        SemanticVersion.Compare(lesser, greater).Should().BeNegative();
        SemanticVersion.Compare(greater, greater).Should().Be(0);
    }

    [Theory]
    [InlineData("4.0.0", "4.0.0-ci")]            // final > pre-release
    [InlineData("4.0.0", "4.0.0-preview.1")]
    [InlineData("1.0.0", "1.0.0-rc.1")]
    public void Compare_FinalBeatsPreRelease(string final, string pre)
    {
        SemanticVersion.Compare(final, pre).Should().BePositive();
        SemanticVersion.Compare(pre, final).Should().BeNegative();
    }

    [Fact]
    public void Compare_PreRelease_SpecExample_IsOrdered()
    {
        // From semver.org §11
        var ordered = new[]
        {
            "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta",
            "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0"
        };
        for (var i = 0; i < ordered.Length - 1; i++)
            SemanticVersion.Compare(ordered[i], ordered[i + 1])
                .Should().BeNegative($"{ordered[i]} should precede {ordered[i + 1]}");
    }

    [Fact]
    public void Compare_NumericIdentifier_LowerThanAlphanumeric()
    {
        // 1.0.0-1 < 1.0.0-alpha (numeric identifiers have lower precedence)
        SemanticVersion.Compare("1.0.0-1", "1.0.0-alpha").Should().BeNegative();
    }

    [Fact]
    public void Compare_CiVsPreview_IsAlphabeticalBySemver()
    {
        // Documents WHY semver alone doesn't help cross-channel: "ci" < "preview" lexically,
        // so preview.1 ranks higher. The channel filter (not the comparer) is what prevents
        // offering it as an upgrade.
        SemanticVersion.Compare("4.0.0-preview.1", "4.0.0-ci").Should().BePositive();
    }

    [Theory]
    [InlineData("4.0.0-ci", "ci")]
    [InlineData("4.0.0-preview.1", "preview")]
    [InlineData("4.0.0-rc.2", "rc")]
    [InlineData("4.0.0-beta3", "beta")]
    [InlineData("v1.2.3-alpha.7+build9", "alpha")]
    [InlineData("4.0.0", "")]            // stable / final
    [InlineData("", "")]
    public void Channel_Extraction(string version, string expected)
    {
        SemanticVersion.Channel(version).Should().Be(expected);
    }

    [Fact]
    public void SameChannel()
    {
        SemanticVersion.SameChannel("4.0.0-ci", "4.0.1-ci").Should().BeTrue();
        SemanticVersion.SameChannel("4.0.0-ci", "4.0.0-preview.1").Should().BeFalse();
        SemanticVersion.SameChannel("4.0.0", "4.1.0").Should().BeTrue();   // both stable
    }
}
