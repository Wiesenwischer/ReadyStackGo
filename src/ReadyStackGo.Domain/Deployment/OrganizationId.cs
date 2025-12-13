namespace ReadyStackGo.Domain.Deployment;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object referencing an Organization from IdentityAccess context.
/// This is a local copy - Deployment context only stores the ID, not the Organization entity.
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
    public static OrganizationId NewId() => new();
    public static OrganizationId FromGuid(Guid value) => new(value);

    /// <summary>
    /// Creates from IdentityAccess OrganizationId (Anti-Corruption Layer conversion).
    /// </summary>
    public static OrganizationId FromIdentityAccess(IdentityAccess.Organizations.OrganizationId identityOrgId)
        => new(identityOrgId.Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
