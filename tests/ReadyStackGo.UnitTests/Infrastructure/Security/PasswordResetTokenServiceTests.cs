using FluentAssertions;
using Microsoft.Extensions.Options;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.UnitTests.Infrastructure.Security;

public class PasswordResetTokenServiceTests
{
    private static readonly JwtSettings Settings = new()
    {
        SecretKey = "this_is_a_test_secret_key_with_enough_length_123456",
        Issuer = "ReadyStackGo",
        Audience = "ReadyStackGo",
        ExpirationMinutes = 60
    };

    private static PasswordResetTokenService CreateSut() =>
        new(Options.Create(Settings));

    [Fact]
    public void Create_then_Validate_ReturnsSameUserId()
    {
        var sut = CreateSut();
        var userId = UserId.NewId();

        var token = sut.Create(userId, TimeSpan.FromHours(1));
        var result = sut.Validate(token);

        result.Should().Be(userId);
    }

    [Fact]
    public void Validate_GarbageToken_ReturnsNull()
    {
        var sut = CreateSut();

        sut.Validate("not-a-token").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankToken_ReturnsNull(string token)
    {
        CreateSut().Validate(token).Should().BeNull();
    }

    [Fact]
    public void Validate_ExpiredToken_ReturnsNull()
    {
        var sut = CreateSut();
        // Negative lifetime → already expired (beyond the 30s clock skew).
        var token = sut.Create(UserId.NewId(), TimeSpan.FromMinutes(-5));

        sut.Validate(token).Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsEmailVerificationToken_PurposeIsolation()
    {
        // A token issued for email verification must not be usable as a password-reset token.
        var verifyService = new EmailVerificationTokenService(Options.Create(Settings));
        var resetService = CreateSut();
        var userId = UserId.NewId();

        var verifyToken = verifyService.Create(userId, TimeSpan.FromHours(1));

        resetService.Validate(verifyToken).Should().BeNull();
    }
}
