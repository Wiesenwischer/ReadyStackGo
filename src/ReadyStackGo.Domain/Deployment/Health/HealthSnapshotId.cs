namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying a HealthSnapshot.
/// </summary>
public sealed class HealthSnapshotId : ValueObject
{
    public Guid Value { get; }

    public HealthSnapshotId()
    {
        Value = Guid.NewGuid();
    }

    public HealthSnapshotId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "HealthSnapshotId cannot be empty.");
        Value = value;
    }

    public static HealthSnapshotId Create() => new();
    public static HealthSnapshotId NewId() => new();
    public static HealthSnapshotId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
