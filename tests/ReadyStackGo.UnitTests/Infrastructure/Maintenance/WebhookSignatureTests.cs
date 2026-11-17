using System.Text.RegularExpressions;
using FluentAssertions;
using ReadyStackGo.Infrastructure.Services.Health;

namespace ReadyStackGo.UnitTests.Infrastructure.Maintenance;

public class WebhookSignatureTests
{
    [Fact]
    public void Compute_HasSha256PrefixAndHexDigest()
    {
        var sig = WebhookSignature.Compute("secret", "{\"state\":\"maintenance\"}");

        sig.Should().StartWith("sha256=");
        Regex.IsMatch(sig, "^sha256=[0-9a-f]{64}$").Should().BeTrue();
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        var a = WebhookSignature.Compute("secret", "body");
        var b = WebhookSignature.Compute("secret", "body");

        a.Should().Be(b);
    }

    [Fact]
    public void Compute_DiffersBySecretAndBody()
    {
        var baseline = WebhookSignature.Compute("secret", "body");

        WebhookSignature.Compute("other", "body").Should().NotBe(baseline);
        WebhookSignature.Compute("secret", "other").Should().NotBe(baseline);
    }

    [Fact]
    public void Compute_MatchesKnownVector()
    {
        // HMAC-SHA256(key="key", msg="The quick brown fox jumps over the lazy dog")
        var sig = WebhookSignature.Compute("key", "The quick brown fox jumps over the lazy dog");

        sig.Should().Be("sha256=f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8");
    }
}
