namespace ReadyStackGo.Domain.Deployment.RuntimeConfig;

/// <summary>
/// Strongly-typed ID for RuntimeStackConfig entities.
/// </summary>
public record RuntimeStackConfigId
{
    public Guid Value { get; }

    public RuntimeStackConfigId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("RuntimeStackConfigId cannot be empty.", nameof(value));

        Value = value;
    }

    public static RuntimeStackConfigId Create(Guid value) => new(value);

    public static RuntimeStackConfigId NewId() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
