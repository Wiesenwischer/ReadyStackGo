using FluentAssertions;
using Moq;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for HashedPassword value object.
/// </summary>
public class HashedPasswordTests
{
    private readonly Mock<IPasswordHasher> _hasherMock;

    public HashedPasswordTests()
    {
        _hasherMock = new Mock<IPasswordHasher>();
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(p => $"hashed_{p}");
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((plain, hash) => $"hashed_{plain}" == hash);
    }

    #region Create Tests

    [Fact]
    public void Create_WithValidPassword_ReturnsHashedPassword()
    {
        // Act
        var password = HashedPassword.Create("ValidPass1", _hasherMock.Object);

        // Assert
        password.Should().NotBeNull();
        password.Hash.Should().Be("hashed_ValidPass1");
    }

    [Fact]
    public void Create_CallsHasherHash()
    {
        // Act
        HashedPassword.Create("ValidPass1", _hasherMock.Object);

        // Assert
        _hasherMock.Verify(h => h.Hash("ValidPass1"), Times.Once);
    }

    [Fact]
    public void Create_WithNullHasher_ThrowsArgumentException()
    {
        // Act
        var act = () => HashedPassword.Create("ValidPass1", null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*hasher*");
    }

    #endregion

    #region Password Strength Validation Tests

    [Fact]
    public void Create_WithEmptyPassword_ThrowsArgumentException()
    {
        // Act
        var act = () => HashedPassword.Create("", _hasherMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Password is required*");
    }

    [Fact]
    public void Create_WithNullPassword_ThrowsArgumentException()
    {
        // Act
        var act = () => HashedPassword.Create(null!, _hasherMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Short1")]       // Too short (6 chars)
    [InlineData("Pass12")]       // Too short (6 chars)
    [InlineData("Passwor")]      // Too short (7 chars)
    public void Create_WithTooShortPassword_ThrowsArgumentException(string shortPassword)
    {
        // Act
        var act = () => HashedPassword.Create(shortPassword, _hasherMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 8 characters*");
    }

    [Theory]
    [InlineData("password1")]    // No uppercase
    [InlineData("alllower1")]    // No uppercase
    public void Create_WithoutUppercase_ThrowsArgumentException(string password)
    {
        // Act
        var act = () => HashedPassword.Create(password, _hasherMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*uppercase letter*");
    }

    [Theory]
    [InlineData("PASSWORD1")]    // No lowercase
    [InlineData("ALLUPPER1")]    // No lowercase
    public void Create_WithoutLowercase_ThrowsArgumentException(string password)
    {
        // Act
        var act = () => HashedPassword.Create(password, _hasherMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*lowercase letter*");
    }

    [Theory]
    [InlineData("Password")]     // No digit
    [InlineData("NoDigits")]     // No digit
    public void Create_WithoutDigit_ThrowsArgumentException(string password)
    {
        // Act
        var act = () => HashedPassword.Create(password, _hasherMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*digit*");
    }

    [Theory]
    [InlineData("ValidPa1")]     // Exactly 8 chars
    [InlineData("Password1")]    // 9 chars
    [InlineData("VeryLongPassword123")]  // Many chars
    [InlineData("Complex1Password")]     // Mixed
    public void Create_WithValidPassword_Succeeds(string validPassword)
    {
        // Act
        var password = HashedPassword.Create(validPassword, _hasherMock.Object);

        // Assert
        password.Should().NotBeNull();
        password.Hash.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region FromHash Tests

    [Fact]
    public void FromHash_WithValidHash_ReturnsHashedPassword()
    {
        // Act
        var password = HashedPassword.FromHash("existinghash");

        // Assert
        password.Should().NotBeNull();
        password.Hash.Should().Be("existinghash");
    }

    [Fact]
    public void FromHash_WithEmptyHash_ThrowsArgumentException()
    {
        // Act
        var act = () => HashedPassword.FromHash("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*hash*required*");
    }

    [Fact]
    public void FromHash_DoesNotValidatePasswordStrength()
    {
        // FromHash is used for existing hashes from storage, no validation needed
        // Act
        var password = HashedPassword.FromHash("anyhash");

        // Assert
        password.Hash.Should().Be("anyhash");
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = HashedPassword.Create("ValidPass1", _hasherMock.Object);

        // Act
        var result = password.Verify("ValidPass1", _hasherMock.Object);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = HashedPassword.Create("ValidPass1", _hasherMock.Object);

        // Act
        var result = password.Verify("WrongPass1", _hasherMock.Object);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_CallsHasherVerify()
    {
        // Arrange
        var password = HashedPassword.Create("ValidPass1", _hasherMock.Object);

        // Act
        password.Verify("TestPass1", _hasherMock.Object);

        // Assert
        _hasherMock.Verify(h => h.Verify("TestPass1", "hashed_ValidPass1"), Times.Once);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameHash_ReturnsTrue()
    {
        // Arrange
        var password1 = HashedPassword.FromHash("samehash");
        var password2 = HashedPassword.FromHash("samehash");

        // Assert
        password1.Should().Be(password2);
    }

    [Fact]
    public void Equals_DifferentHash_ReturnsFalse()
    {
        // Arrange
        var password1 = HashedPassword.FromHash("hash1");
        var password2 = HashedPassword.FromHash("hash2");

        // Assert
        password1.Should().NotBe(password2);
    }

    [Fact]
    public void GetHashCode_SameHash_ReturnsSameHashCode()
    {
        // Arrange
        var password1 = HashedPassword.FromHash("samehash");
        var password2 = HashedPassword.FromHash("samehash");

        // Assert
        password1.GetHashCode().Should().Be(password2.GetHashCode());
    }

    #endregion
}
