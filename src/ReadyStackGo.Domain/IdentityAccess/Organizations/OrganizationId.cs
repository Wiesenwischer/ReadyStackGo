namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying an Organization.
/// </summary>
public sealed class OrganizationId : ValueObject
{
    public Guid Value { get; }

    public OrganizationId()
    {
        Value = Guid.NewGuid();
    }

    public OrganizationId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "OrganizationId cannot be empty.");
        Value = value;
    }

    public static OrganizationId Create() => new();
    public static OrganizationId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
