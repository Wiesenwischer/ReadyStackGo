using FluentAssertions;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.UnitTests.Authentication;

public class ApiKeyHasherTests
{
    [Fact]
    public void ComputeSha256Hash_SameInput_ReturnsSameHash()
    {
        var key = "rsgo_test1234567890";

        var hash1 = ApiKeyHasher.ComputeSha256Hash(key);
        var hash2 = ApiKeyHasher.ComputeSha256Hash(key);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeSha256Hash_Returns64CharLowercaseHex()
    {
        var key = "rsgo_someapikey";

        var hash = ApiKeyHasher.ComputeSha256Hash(key);

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeSha256Hash_DifferentInputs_ReturnDifferentHashes()
    {
        var hash1 = ApiKeyHasher.ComputeSha256Hash("rsgo_key_one");
        var hash2 = ApiKeyHasher.ComputeSha256Hash("rsgo_key_two");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeSha256Hash_EmptyString_ReturnsValidHash()
    {
        var hash = ApiKeyHasher.ComputeSha256Hash("");

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }
}
