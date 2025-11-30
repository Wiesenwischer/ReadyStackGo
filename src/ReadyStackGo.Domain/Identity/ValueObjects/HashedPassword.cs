namespace ReadyStackGo.Domain.Identity.ValueObjects;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.Services;

/// <summary>
/// Value object representing a securely hashed password.
/// </summary>
public sealed class HashedPassword : ValueObject
{
    public string Hash { get; private set; }

    // For EF Core
    private HashedPassword() => Hash = null!;

    private HashedPassword(string hash)
    {
        SelfAssertArgumentNotEmpty(hash, "Password hash is required.");
        Hash = hash;
    }

    public static HashedPassword Create(string plainTextPassword, IPasswordHasher hasher)
    {
        AssertionConcern.AssertArgumentNotNull(hasher, "Password hasher is required.");
        ValidatePasswordStrength(plainTextPassword);

        var hash = hasher.Hash(plainTextPassword);
        return new HashedPassword(hash);
    }

    public static HashedPassword FromHash(string hash)
    {
        return new HashedPassword(hash);
    }

    public bool Verify(string plainTextPassword, IPasswordHasher hasher)
    {
        return hasher.Verify(plainTextPassword, Hash);
    }

    private static void ValidatePasswordStrength(string password)
    {
        AssertionConcern.AssertArgumentNotEmpty(password, "Password is required.");
        AssertionConcern.AssertArgumentTrue(
            password.Length >= 8,
            "Password must be at least 8 characters.");
        AssertionConcern.AssertArgumentTrue(
            password.Any(char.IsUpper),
            "Password must contain at least one uppercase letter.");
        AssertionConcern.AssertArgumentTrue(
            password.Any(char.IsLower),
            "Password must contain at least one lowercase letter.");
        AssertionConcern.AssertArgumentTrue(
            password.Any(char.IsDigit),
            "Password must contain at least one digit.");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Hash;
    }
}
