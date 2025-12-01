namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object representing a validated email address.
/// </summary>
public sealed class EmailAddress : ValueObject
{
    private const string EmailPattern = @"^[\w\.-]+@[\w\.-]+\.\w+$";

    public string Value { get; private set; }

    // For EF Core
    private EmailAddress() => Value = null!;

    public EmailAddress(string address)
    {
        SelfAssertArgumentNotEmpty(address, "Email address is required.");
        SelfAssertArgumentLength(address, 1, 254, "Email address must be 254 characters or less.");
        SelfAssertArgumentMatches(EmailPattern, address, "Email format is invalid.");

        Value = address.ToLowerInvariant();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
