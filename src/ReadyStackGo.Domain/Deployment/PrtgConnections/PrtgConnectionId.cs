namespace ReadyStackGo.Domain.Deployment.PrtgConnections;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying a <see cref="PrtgConnection"/>.
/// </summary>
public sealed class PrtgConnectionId : ValueObject
{
    public Guid Value { get; }

    public PrtgConnectionId()
    {
        Value = Guid.NewGuid();
    }

    public PrtgConnectionId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "PrtgConnectionId cannot be empty.");
        Value = value;
    }

    public static PrtgConnectionId Create() => new();
    public static PrtgConnectionId NewId() => new();
    public static PrtgConnectionId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
