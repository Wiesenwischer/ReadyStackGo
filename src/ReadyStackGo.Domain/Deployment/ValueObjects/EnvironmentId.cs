namespace ReadyStackGo.Domain.Deployment.ValueObjects;

using ReadyStackGo.Domain.Common;

/// <summary>
/// Value object identifying an Environment.
/// </summary>
public sealed class EnvironmentId : ValueObject
{
    public Guid Value { get; }

    public EnvironmentId()
    {
        Value = Guid.NewGuid();
    }

    public EnvironmentId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "EnvironmentId cannot be empty.");
        Value = value;
    }

    public static EnvironmentId Create() => new();
    public static EnvironmentId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
