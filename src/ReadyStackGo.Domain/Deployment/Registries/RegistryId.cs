namespace ReadyStackGo.Domain.Deployment.Registries;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying a Docker Registry.
/// </summary>
public sealed class RegistryId : ValueObject
{
    public Guid Value { get; }

    public RegistryId()
    {
        Value = Guid.NewGuid();
    }

    public RegistryId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "RegistryId cannot be empty.");
        Value = value;
    }

    public static RegistryId Create() => new();
    public static RegistryId NewId() => new();
    public static RegistryId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
