using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.UnitTests.TestSupport;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

public class UsernameGeneratorTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly UsernameGenerator _sut;

    public UsernameGeneratorTests()
    {
        _sut = new UsernameGenerator(_users);
    }

    private void AddUser(string username) =>
        _users.Add(User.Register(_users.NextIdentity(), username, new EmailAddress($"{Guid.NewGuid():N}@example.com"), HashedPassword.FromHash("h")));

    [Fact]
    public void Constructor_NullRepository_Throws()
    {
        var act = () => new UsernameGenerator(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("userRepository");
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("Alice_2")]
    [InlineData("abc")]
    public void Generate_ValidPreferredUsername_IsUsedUnchanged(string preferred)
    {
        _sut.Generate(preferred, "x@example.com").Should().Be(preferred);
    }

    [Theory]
    [InlineData("first.last", "first_last")]
    [InlineData("first-last", "first_last")]
    [InlineData("first+tag", "first_tag")]
    [InlineData("jürgen", "j_rgen")]
    [InlineData("  padded  ", "padded")]
    public void Generate_InvalidCharacters_AreReplacedWithUnderscore(string preferred, string expected)
    {
        _sut.Generate(preferred, null).Should().Be(expected);
    }

    [Theory]
    [InlineData("a", "a00")]
    [InlineData("ab", "ab0")]
    public void Generate_TooShort_IsPaddedToMinimumLength(string preferred, string expected)
    {
        _sut.Generate(preferred, null).Should().Be(expected);
    }

    [Fact]
    public void Generate_TooLong_IsCutToBaseLength()
    {
        var result = _sut.Generate(new string('a', 80), null);

        result.Should().Be(new string('a', UsernameGenerator.MaxBaseLength));
    }

    [Fact]
    public void Generate_Taken_AppendsIncreasingNumber()
    {
        AddUser("alice");
        AddUser("alice1");

        _sut.Generate("alice", null).Should().Be("alice2");
    }

    [Fact]
    public void Generate_TakenIgnoringCase_AppendsNumber()
    {
        AddUser("Alice");

        _sut.Generate("alice", null).Should().Be("alice1");
    }

    [Fact]
    public void Generate_LongNameTakenManyTimes_NeverExceedsMaxLength()
    {
        var baseName = new string('b', UsernameGenerator.MaxBaseLength);
        AddUser(baseName);
        for (var i = 1; i < 120; i++)
        {
            AddUser(baseName + i);
        }

        var result = _sut.Generate(new string('b', 80), null);

        result.Should().Be(baseName + "120");
        result.Length.Should().BeLessThanOrEqualTo(UsernameGenerator.MaxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("___")]
    [InlineData("...")]
    [InlineData("äöü")]
    public void Generate_PreferredUnusable_FallsBackToEmailLocalPart(string? preferred)
    {
        _sut.Generate(preferred, "carol.smith@example.com").Should().Be("carol_smith");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("...@example.com")]
    [InlineData("@example.com")]
    public void Generate_PreferredAndEmailUnusable_UsesFallbackUser(string? email)
    {
        _sut.Generate(null, email).Should().Be("user");
    }

    [Fact]
    public void Generate_FallbackTaken_AppendsNumber()
    {
        AddUser("user");

        _sut.Generate(null, null).Should().Be("user1");
    }

    [Fact]
    public void Generate_ResultAlwaysMatchesUsernameRules()
    {
        var inputs = new[] { "x", "a.b-c+d", new string('z', 200), "ok_name", "--", "Ünïcödé" };

        foreach (var input in inputs)
        {
            var result = _sut.Generate(input, "fallback@example.com");
            result.Should().MatchRegex("^[a-zA-Z0-9_]{3,50}$", $"input was '{input}'");
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("_", null)]
    [InlineData("a_", "a_")]
    [InlineData("a b", "a_b")]
    public void Sanitize_ReturnsNullWhenNothingUsableRemains(string? input, string? expected)
    {
        UsernameGenerator.Sanitize(input).Should().Be(expected);
    }
}
