namespace ReadyStackGo.Domain.Deployment;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object referencing a User from IdentityAccess context.
/// This is a local copy - Deployment context only stores the ID, not the User entity.
/// </summary>
public sealed class UserId : ValueObject
{
    public Guid Value { get; }

    public UserId()
    {
        Value = Guid.NewGuid();
    }

    public UserId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "UserId cannot be empty.");
        Value = value;
    }

    public static UserId Create() => new();
    public static UserId NewId() => new();
    public static UserId FromGuid(Guid value) => new(value);

    /// <summary>
    /// Creates from IdentityAccess UserId (Anti-Corruption Layer conversion).
    /// </summary>
    public static UserId FromIdentityAccess(IdentityAccess.Users.UserId identityUserId)
        => new(identityUserId.Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
