namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Child entity representing a single stack within a product deployment.
/// Exists only in the context of a ProductDeployment aggregate.
/// References the actual Deployment aggregate via DeploymentId (cross-aggregate reference).
/// </summary>
public class ProductStackDeployment
{
    public int Id { get; private set; }
    public string StackName { get; private set; } = null!;
    public string StackDisplayName { get; private set; } = null!;
    public string StackId { get; private set; } = null!;
    public DeploymentId? DeploymentId { get; private set; }
    public string? DeploymentStackName { get; private set; }
    public StackDeploymentStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int Order { get; private set; }
    public int ServiceCount { get; private set; }
    public bool IsNewInUpgrade { get; private set; }

    private readonly Dictionary<string, string> _variables = new();
    public IReadOnlyDictionary<string, string> Variables => _variables;

    // For EF Core
    protected ProductStackDeployment() { }

    public ProductStackDeployment(
        string stackName,
        string stackDisplayName,
        string stackId,
        int order,
        int serviceCount,
        IReadOnlyDictionary<string, string> variables,
        bool isNewInUpgrade = false)
    {
        AssertionConcern.AssertArgumentNotEmpty(stackName, "Stack name is required.");
        AssertionConcern.AssertArgumentNotEmpty(stackDisplayName, "Stack display name is required.");
        AssertionConcern.AssertArgumentNotEmpty(stackId, "Stack ID is required.");
        AssertionConcern.AssertArgumentTrue(order >= 0, "Order must be non-negative.");
        AssertionConcern.AssertArgumentTrue(serviceCount >= 0, "Service count must be non-negative.");

        StackName = stackName;
        StackDisplayName = stackDisplayName;
        StackId = stackId;
        Order = order;
        ServiceCount = serviceCount;
        Status = StackDeploymentStatus.Pending;
        IsNewInUpgrade = isNewInUpgrade;

        if (variables != null)
        {
            foreach (var kvp in variables)
                _variables[kvp.Key] = kvp.Value;
        }
    }

    internal void Start(DeploymentId deploymentId, string deploymentStackName)
    {
        AssertionConcern.AssertArgumentNotNull(deploymentId, "DeploymentId is required.");
        AssertionConcern.AssertArgumentNotEmpty(deploymentStackName, "Deployment stack name is required.");
        AssertionConcern.AssertStateTrue(
            Status == StackDeploymentStatus.Pending,
            $"Cannot start stack '{StackName}': current status is {Status}, expected Pending.");

        DeploymentId = deploymentId;
        DeploymentStackName = deploymentStackName;
        Status = StackDeploymentStatus.Deploying;
        StartedAt = SystemClock.UtcNow;
    }

    internal void Complete()
    {
        AssertionConcern.AssertStateTrue(
            Status == StackDeploymentStatus.Deploying,
            $"Cannot complete stack '{StackName}': current status is {Status}, expected Deploying.");

        Status = StackDeploymentStatus.Running;
        CompletedAt = SystemClock.UtcNow;
    }

    internal void Fail(string errorMessage)
    {
        AssertionConcern.AssertArgumentNotEmpty(errorMessage, "Error message is required.");
        AssertionConcern.AssertStateTrue(
            Status is StackDeploymentStatus.Deploying or StackDeploymentStatus.Pending,
            $"Cannot fail stack '{StackName}': current status is {Status}, expected Pending or Deploying.");

        Status = StackDeploymentStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = SystemClock.UtcNow;
    }

    internal void MarkRemoved()
    {
        Status = StackDeploymentStatus.Removed;
        CompletedAt = SystemClock.UtcNow;
    }

    internal void ResetToPending()
    {
        Status = StackDeploymentStatus.Pending;
        StartedAt = null;
        CompletedAt = null;
        ErrorMessage = null;
    }

    public TimeSpan? GetDuration()
    {
        if (StartedAt == null) return null;
        return (CompletedAt ?? SystemClock.UtcNow) - StartedAt.Value;
    }
}
