namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.SharedKernel;

// ═══════════════════════════════════════════════════════════════════
// Product-Level Events
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Raised when a new product deployment is initiated.
/// </summary>
public sealed class ProductDeploymentInitiated : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public EnvironmentId EnvironmentId { get; }
    public string ProductName { get; }
    public string ProductVersion { get; }
    public int TotalStacks { get; }

    public ProductDeploymentInitiated(
        ProductDeploymentId productDeploymentId,
        EnvironmentId environmentId,
        string productName,
        string productVersion,
        int totalStacks)
    {
        ProductDeploymentId = productDeploymentId;
        EnvironmentId = environmentId;
        ProductName = productName;
        ProductVersion = productVersion;
        TotalStacks = totalStacks;
    }
}

/// <summary>
/// Raised when all stacks in a product deployment have completed successfully.
/// </summary>
public sealed class ProductDeploymentCompleted : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string ProductName { get; }
    public string ProductVersion { get; }
    public int TotalStacks { get; }
    public TimeSpan Duration { get; }

    public ProductDeploymentCompleted(
        ProductDeploymentId productDeploymentId,
        string productName,
        string productVersion,
        int totalStacks,
        TimeSpan duration)
    {
        ProductDeploymentId = productDeploymentId;
        ProductName = productName;
        ProductVersion = productVersion;
        TotalStacks = totalStacks;
        Duration = duration;
    }
}

/// <summary>
/// Raised when a product deployment completes with some stacks running and some failed.
/// </summary>
public sealed class ProductDeploymentPartiallyCompleted : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string ProductName { get; }
    public int RunningStacks { get; }
    public int FailedStacks { get; }
    public string Reason { get; }

    public ProductDeploymentPartiallyCompleted(
        ProductDeploymentId productDeploymentId,
        string productName,
        int runningStacks,
        int failedStacks,
        string reason)
    {
        ProductDeploymentId = productDeploymentId;
        ProductName = productName;
        RunningStacks = runningStacks;
        FailedStacks = failedStacks;
        Reason = reason;
    }
}

/// <summary>
/// Raised when a product deployment fails entirely.
/// </summary>
public sealed class ProductDeploymentFailed : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string ProductName { get; }
    public string ErrorMessage { get; }
    public int CompletedStacks { get; }
    public int FailedStacks { get; }

    public ProductDeploymentFailed(
        ProductDeploymentId productDeploymentId,
        string productName,
        string errorMessage,
        int completedStacks,
        int failedStacks)
    {
        ProductDeploymentId = productDeploymentId;
        ProductName = productName;
        ErrorMessage = errorMessage;
        CompletedStacks = completedStacks;
        FailedStacks = failedStacks;
    }
}

/// <summary>
/// Raised when a product upgrade is initiated.
/// </summary>
public sealed class ProductUpgradeInitiated : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string ProductName { get; }
    public string PreviousVersion { get; }
    public string TargetVersion { get; }
    public int TotalStacks { get; }

    public ProductUpgradeInitiated(
        ProductDeploymentId productDeploymentId,
        string productName,
        string previousVersion,
        string targetVersion,
        int totalStacks)
    {
        ProductDeploymentId = productDeploymentId;
        ProductName = productName;
        PreviousVersion = previousVersion;
        TargetVersion = targetVersion;
        TotalStacks = totalStacks;
    }
}

/// <summary>
/// Raised when product removal is initiated.
/// </summary>
public sealed class ProductRemovalInitiated : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string ProductName { get; }
    public int TotalStacks { get; }

    public ProductRemovalInitiated(
        ProductDeploymentId productDeploymentId,
        string productName,
        int totalStacks)
    {
        ProductDeploymentId = productDeploymentId;
        ProductName = productName;
        TotalStacks = totalStacks;
    }
}

/// <summary>
/// Raised when all stacks have been removed (terminal state).
/// </summary>
public sealed class ProductDeploymentRemoved : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string ProductName { get; }

    public ProductDeploymentRemoved(
        ProductDeploymentId productDeploymentId,
        string productName)
    {
        ProductDeploymentId = productDeploymentId;
        ProductName = productName;
    }
}

// ═══════════════════════════════════════════════════════════════════
// Stack-Level Events
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Raised when a stack deployment within a product deployment is started.
/// </summary>
public sealed class ProductStackDeploymentStarted : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string StackName { get; }
    public DeploymentId DeploymentId { get; }
    public int StackIndex { get; }
    public int TotalStacks { get; }

    public ProductStackDeploymentStarted(
        ProductDeploymentId productDeploymentId,
        string stackName,
        DeploymentId deploymentId,
        int stackIndex,
        int totalStacks)
    {
        ProductDeploymentId = productDeploymentId;
        StackName = stackName;
        DeploymentId = deploymentId;
        StackIndex = stackIndex;
        TotalStacks = totalStacks;
    }
}

/// <summary>
/// Raised when a stack deployment within a product deployment completes successfully.
/// </summary>
public sealed class ProductStackDeploymentCompleted : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string StackName { get; }
    public DeploymentId DeploymentId { get; }
    public int CompletedStacks { get; }
    public int TotalStacks { get; }

    public ProductStackDeploymentCompleted(
        ProductDeploymentId productDeploymentId,
        string stackName,
        DeploymentId deploymentId,
        int completedStacks,
        int totalStacks)
    {
        ProductDeploymentId = productDeploymentId;
        StackName = stackName;
        DeploymentId = deploymentId;
        CompletedStacks = completedStacks;
        TotalStacks = totalStacks;
    }
}

/// <summary>
/// Raised when a stack deployment within a product deployment fails.
/// </summary>
public sealed class ProductStackDeploymentFailed : DomainEvent
{
    public ProductDeploymentId ProductDeploymentId { get; }
    public string StackName { get; }
    public string ErrorMessage { get; }
    public int CompletedStacks { get; }
    public int TotalStacks { get; }

    public ProductStackDeploymentFailed(
        ProductDeploymentId productDeploymentId,
        string stackName,
        string errorMessage,
        int completedStacks,
        int totalStacks)
    {
        ProductDeploymentId = productDeploymentId;
        StackName = stackName;
        ErrorMessage = errorMessage;
        CompletedStacks = completedStacks;
        TotalStacks = totalStacks;
    }
}
