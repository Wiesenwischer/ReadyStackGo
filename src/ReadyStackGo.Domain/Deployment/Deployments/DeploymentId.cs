namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying a Deployment.
/// </summary>
public sealed class DeploymentId : ValueObject
{
    public Guid Value { get; }

    public DeploymentId()
    {
        Value = Guid.NewGuid();
    }

    public DeploymentId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "DeploymentId cannot be empty.");
        Value = value;
    }

    public static DeploymentId Create() => new();
    public static DeploymentId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
