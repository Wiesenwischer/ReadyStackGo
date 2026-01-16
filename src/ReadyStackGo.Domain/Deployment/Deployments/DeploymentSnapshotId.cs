namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying a Deployment Snapshot.
/// </summary>
public sealed class DeploymentSnapshotId : ValueObject
{
    public Guid Value { get; }

    public DeploymentSnapshotId()
    {
        Value = Guid.NewGuid();
    }

    public DeploymentSnapshotId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "DeploymentSnapshotId cannot be empty.");
        Value = value;
    }

    public static DeploymentSnapshotId Create() => new();
    public static DeploymentSnapshotId NewId() => new();
    public static DeploymentSnapshotId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
