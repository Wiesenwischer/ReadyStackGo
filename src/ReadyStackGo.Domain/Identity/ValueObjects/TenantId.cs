namespace ReadyStackGo.Domain.Identity.ValueObjects;

using ReadyStackGo.Domain.Common;

/// <summary>
/// Value object identifying a Tenant.
/// </summary>
public sealed class TenantId : ValueObject
{
    public Guid Value { get; }

    public TenantId()
    {
        Value = Guid.NewGuid();
    }

    public TenantId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "TenantId cannot be empty.");
        Value = value;
    }

    public static TenantId Create() => new();
    public static TenantId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
