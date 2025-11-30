namespace ReadyStackGo.Domain.Access.ValueObjects;

using ReadyStackGo.Domain.Common;

/// <summary>
/// Value object identifying a Role.
/// </summary>
public sealed class RoleId : ValueObject
{
    public string Value { get; }

    public RoleId(string value)
    {
        SelfAssertArgumentNotEmpty(value, "RoleId cannot be empty.");
        SelfAssertArgumentLength(value, 1, 50, "RoleId must be 50 characters or less.");
        Value = value;
    }

    public static RoleId SystemAdmin => new("SystemAdmin");
    public static RoleId OrganizationOwner => new("OrganizationOwner");
    public static RoleId Operator => new("Operator");
    public static RoleId Viewer => new("Viewer");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
