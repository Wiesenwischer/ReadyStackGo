namespace ReadyStackGo.Domain.IdentityAccess.ValueObjects;

using ReadyStackGo.Domain.Common;

/// <summary>
/// Value object identifying a User.
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
    public static UserId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
