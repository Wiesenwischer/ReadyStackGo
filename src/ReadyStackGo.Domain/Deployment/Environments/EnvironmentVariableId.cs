namespace ReadyStackGo.Domain.Deployment.Environments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying an EnvironmentVariable.
/// </summary>
public sealed class EnvironmentVariableId : ValueObject
{
    public Guid Value { get; }

    public EnvironmentVariableId()
    {
        Value = Guid.NewGuid();
    }

    public EnvironmentVariableId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "EnvironmentVariableId cannot be empty.");
        Value = value;
    }

    public static EnvironmentVariableId Create() => new();
    public static EnvironmentVariableId NewId() => new();
    public static EnvironmentVariableId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
