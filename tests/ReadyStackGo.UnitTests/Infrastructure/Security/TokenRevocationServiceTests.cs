using FluentAssertions;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.UnitTests.Infrastructure.Security;

public class TokenRevocationServiceTests
{
    [Fact]
    public void Revoke_then_IsRevoked_ReturnsTrue()
    {
        var sut = new TokenRevocationService();

        sut.Revoke("jti-1", DateTimeOffset.UtcNow.AddHours(1));

        sut.IsRevoked("jti-1").Should().BeTrue();
    }

    [Fact]
    public void IsRevoked_UnknownToken_ReturnsFalse()
    {
        var sut = new TokenRevocationService();

        sut.IsRevoked("never-seen").Should().BeFalse();
    }

    [Fact]
    public void IsRevoked_ExpiredRevocation_ReturnsFalse()
    {
        var sut = new TokenRevocationService();

        // Already past expiry — the lifetime check would reject it anyway.
        sut.Revoke("jti-old", DateTimeOffset.UtcNow.AddMinutes(-5));

        sut.IsRevoked("jti-old").Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Revoke_BlankJti_IsNoOp(string? jti)
    {
        var sut = new TokenRevocationService();

        sut.Revoke(jti!, DateTimeOffset.UtcNow.AddHours(1));

        sut.IsRevoked(jti!).Should().BeFalse();
    }

    [Fact]
    public void Revoke_DoesNotAffectOtherTokens()
    {
        var sut = new TokenRevocationService();

        sut.Revoke("jti-1", DateTimeOffset.UtcNow.AddHours(1));

        sut.IsRevoked("jti-2").Should().BeFalse();
    }
}
