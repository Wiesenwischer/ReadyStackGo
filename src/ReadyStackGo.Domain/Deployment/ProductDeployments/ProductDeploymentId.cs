namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying a ProductDeployment.
/// </summary>
public sealed class ProductDeploymentId : ValueObject
{
    public Guid Value { get; }

    public ProductDeploymentId()
    {
        Value = Guid.NewGuid();
    }

    public ProductDeploymentId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "ProductDeploymentId cannot be empty.");
        Value = value;
    }

    public static ProductDeploymentId Create() => new();
    public static ProductDeploymentId NewId() => new();
    public static ProductDeploymentId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
