using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for EmailAddress value object.
/// </summary>
public class EmailAddressTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidEmail_CreatesEmailAddress()
    {
        // Act
        var email = new EmailAddress("test@example.com");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_NormalizesToLowercase()
    {
        // Act
        var email = new EmailAddress("TEST@EXAMPLE.COM");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Theory]
    [InlineData("user@domain.com")]
    [InlineData("user.name@domain.com")]
    [InlineData("user-name@domain.co.uk")]
    [InlineData("user_name@domain.org")]
    [InlineData("user123@domain123.com")]
    public void Constructor_WithValidFormats_Succeeds(string validEmail)
    {
        // Act
        var email = new EmailAddress(validEmail);

        // Assert
        email.Value.Should().Be(validEmail.ToLowerInvariant());
    }

    [Fact]
    public void Constructor_WithEmptyString_ThrowsArgumentException()
    {
        // Act
        var act = () => new EmailAddress("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentException()
    {
        // Act
        var act = () => new EmailAddress(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("missing@domain")]
    [InlineData("@domain.com")]
    [InlineData("user@")]
    [InlineData("user@.com")]
    public void Constructor_WithInvalidFormat_ThrowsArgumentException(string invalidEmail)
    {
        // Act
        var act = () => new EmailAddress(invalidEmail);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithTooLongEmail_ThrowsArgumentException()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@example.com";

        // Act
        var act = () => new EmailAddress(longEmail);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameEmailValue_ReturnsTrue()
    {
        // Arrange
        var email1 = new EmailAddress("test@example.com");
        var email2 = new EmailAddress("test@example.com");

        // Assert
        email1.Should().Be(email2);
    }

    [Fact]
    public void Equals_DifferentCase_ReturnsTrue()
    {
        // Arrange
        var email1 = new EmailAddress("test@example.com");
        var email2 = new EmailAddress("TEST@EXAMPLE.COM");

        // Assert
        email1.Should().Be(email2);
    }

    [Fact]
    public void Equals_DifferentEmails_ReturnsFalse()
    {
        // Arrange
        var email1 = new EmailAddress("test1@example.com");
        var email2 = new EmailAddress("test2@example.com");

        // Assert
        email1.Should().NotBe(email2);
    }

    [Fact]
    public void GetHashCode_SameEmail_ReturnsSameHash()
    {
        // Arrange
        var email1 = new EmailAddress("test@example.com");
        var email2 = new EmailAddress("test@example.com");

        // Assert
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsEmailValue()
    {
        // Arrange
        var email = new EmailAddress("test@example.com");

        // Act
        var result = email.ToString();

        // Assert
        result.Should().Be("test@example.com");
    }

    #endregion
}
