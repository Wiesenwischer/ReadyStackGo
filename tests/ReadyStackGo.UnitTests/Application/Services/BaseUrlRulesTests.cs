using FluentAssertions;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.UnitTests.Application.Services;

public class BaseUrlRulesTests
{
    #region IsUnset

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://localhost:5000")]
    [InlineData("http://localhost:5000/")]
    [InlineData("HTTP://LOCALHOST:5000")]
    [InlineData(" http://localhost:5000 ")]
    public void IsUnset_EmptyOrBuiltInDefault_True(string? value)
    {
        BaseUrlRules.IsUnset(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("https://localhost:5000")]
    [InlineData("http://localhost:5000/rsgo")]
    [InlineData("https://rsgo.example.com")]
    public void IsUnset_RealValue_False(string value)
    {
        BaseUrlRules.IsUnset(value).Should().BeFalse();
    }

    #endregion

    #region Normalize

    [Theory]
    [InlineData("https://rsgo.example.com", "https://rsgo.example.com")]
    [InlineData("https://rsgo.example.com/", "https://rsgo.example.com")]
    [InlineData("  https://rsgo.example.com/  ", "https://rsgo.example.com")]
    [InlineData("HTTPS://RSGO.Example.COM", "https://rsgo.example.com")]
    [InlineData("http://server:8080/", "http://server:8080")]
    [InlineData("https://rsgo.example.com:443", "https://rsgo.example.com")]
    [InlineData("https://example.com/rsgo/", "https://example.com/rsgo")]
    [InlineData("http://[::1]:8080", "http://[::1]:8080")]
    public void Normalize_ValidValues_AreNormalized(string input, string expected)
    {
        BaseUrlRules.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("rsgo.example.com")]
    [InlineData("/relative")]
    [InlineData("ftp://rsgo.example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://rsgo.example.com/?a=b")]
    [InlineData("https://rsgo.example.com/#frag")]
    [InlineData("https://user:pass@rsgo.example.com")]
    [InlineData("https://")]
    public void Normalize_InvalidValues_ReturnNull(string? input)
    {
        BaseUrlRules.Normalize(input).Should().BeNull();
    }

    #endregion

    #region HTTPS rule

    [Theory]
    [InlineData("https://rsgo.example.com", true)]
    [InlineData("https://server:8443", true)]
    [InlineData("http://localhost", true)]
    [InlineData("http://localhost:8080", true)]
    [InlineData("http://LOCALHOST:8080", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    [InlineData("http://127.1.2.3", true)]
    [InlineData("http://[::1]:8080", true)]
    [InlineData("http://server:8080", false)]
    [InlineData("http://localhost.example.com", false)]
    [InlineData("http://localhost.example.com:8080", false)]
    [InlineData("http://192.168.1.10:8080", false)]
    [InlineData("http://0.0.0.0:8080", false)]
    [InlineData("http://[::2]:8080", false)]
    [InlineData("ftp://localhost", false)]
    [InlineData("not a url", false)]
    [InlineData("", false)]
    public void SatisfiesHttpsRequirement(string baseUrl, bool expected)
    {
        BaseUrlRules.SatisfiesHttpsRequirement(baseUrl).Should().Be(expected);
    }

    #endregion

    [Theory]
    [InlineData("https://rsgo.example.com", "https://rsgo.example.com/api/auth/oidc/wysch/callback")]
    [InlineData("https://rsgo.example.com/", "https://rsgo.example.com/api/auth/oidc/wysch/callback")]
    public void ProviderRedirectUri_BuildsCallbackWithoutDoubleSlash(string baseUrl, string expected)
    {
        BaseUrlRules.ProviderRedirectUri(baseUrl, "wysch").Should().Be(expected);
    }
}
