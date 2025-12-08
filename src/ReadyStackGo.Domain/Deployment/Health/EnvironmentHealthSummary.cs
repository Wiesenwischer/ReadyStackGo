namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object representing aggregated health status for all deployments in an environment.
/// This is a domain concept that encapsulates the aggregation logic.
/// </summary>
public sealed class EnvironmentHealthSummary : ValueObject
{
    /// <summary>
    /// The environment this summary is for.
    /// </summary>
    public EnvironmentId EnvironmentId { get; }

    /// <summary>
    /// Name of the environment.
    /// </summary>
    public string EnvironmentName { get; }

    /// <summary>
    /// Overall aggregated health status of the environment.
    /// </summary>
    public HealthStatus OverallStatus { get; }

    /// <summary>
    /// Total number of stacks in the environment.
    /// </summary>
    public int TotalStacks { get; }

    /// <summary>
    /// Number of healthy stacks.
    /// </summary>
    public int HealthyCount { get; }

    /// <summary>
    /// Number of degraded stacks.
    /// </summary>
    public int DegradedCount { get; }

    /// <summary>
    /// Number of unhealthy stacks.
    /// </summary>
    public int UnhealthyCount { get; }

    /// <summary>
    /// Number of stacks with unknown status.
    /// </summary>
    public int UnknownCount { get; }

    /// <summary>
    /// Individual stack health summaries.
    /// </summary>
    public IReadOnlyList<StackHealthSummary> Stacks { get; }

    /// <summary>
    /// Timestamp when this summary was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; }

    private EnvironmentHealthSummary(
        EnvironmentId environmentId,
        string environmentName,
        IReadOnlyList<StackHealthSummary> stacks)
    {
        EnvironmentId = environmentId;
        EnvironmentName = environmentName;
        Stacks = stacks;
        CreatedAtUtc = DateTime.UtcNow;

        // Calculate counts
        TotalStacks = stacks.Count;
        HealthyCount = stacks.Count(s => s.OverallStatus == HealthStatus.Healthy);
        DegradedCount = stacks.Count(s => s.OverallStatus == HealthStatus.Degraded);
        UnhealthyCount = stacks.Count(s => s.OverallStatus == HealthStatus.Unhealthy);
        UnknownCount = stacks.Count(s => s.OverallStatus == HealthStatus.Unknown);

        // Calculate overall status
        OverallStatus = CalculateOverallStatus();
    }

    /// <summary>
    /// Creates an environment health summary from a collection of health snapshots.
    /// </summary>
    public static EnvironmentHealthSummary FromSnapshots(
        Environment environment,
        IEnumerable<HealthSnapshot> snapshots)
    {
        var stackSummaries = snapshots
            .Select(StackHealthSummary.FromSnapshot)
            .ToList();

        return new EnvironmentHealthSummary(
            environment.Id,
            environment.Name,
            stackSummaries);
    }

    /// <summary>
    /// Creates an empty summary for an environment with no deployments.
    /// </summary>
    public static EnvironmentHealthSummary Empty(Environment environment)
    {
        return new EnvironmentHealthSummary(
            environment.Id,
            environment.Name,
            Array.Empty<StackHealthSummary>());
    }

    /// <summary>
    /// Calculates the overall environment health based on stack health.
    /// Uses the "worst status wins" aggregation strategy.
    /// </summary>
    private HealthStatus CalculateOverallStatus()
    {
        if (TotalStacks == 0)
            return HealthStatus.Unknown;

        if (UnhealthyCount > 0)
            return HealthStatus.Unhealthy;

        if (DegradedCount > 0)
            return HealthStatus.Degraded;

        if (UnknownCount > 0)
            return HealthStatus.Unknown;

        return HealthStatus.Healthy;
    }

    /// <summary>
    /// Indicates if this environment requires attention.
    /// </summary>
    public bool RequiresAttention =>
        UnhealthyCount > 0 || DegradedCount > 0 ||
        Stacks.Any(s => s.RequiresAttention);

    /// <summary>
    /// Gets a human-readable status message.
    /// </summary>
    public string GetStatusMessage()
    {
        if (TotalStacks == 0)
            return "No deployments";

        if (UnhealthyCount > 0)
            return $"{UnhealthyCount} of {TotalStacks} stack(s) unhealthy";

        if (DegradedCount > 0)
            return $"{DegradedCount} of {TotalStacks} stack(s) degraded";

        if (UnknownCount > 0)
            return $"{UnknownCount} of {TotalStacks} stack(s) with unknown status";

        return $"All {TotalStacks} stack(s) healthy";
    }

    /// <summary>
    /// Gets stacks that require attention (unhealthy or degraded).
    /// </summary>
    public IEnumerable<StackHealthSummary> GetStacksRequiringAttention()
    {
        return Stacks.Where(s => s.RequiresAttention);
    }

    /// <summary>
    /// Gets the percentage of healthy stacks.
    /// </summary>
    public double HealthyPercentage =>
        TotalStacks > 0 ? (double)HealthyCount / TotalStacks * 100 : 0;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EnvironmentId;
        yield return EnvironmentName;
        yield return TotalStacks;
        yield return HealthyCount;
        yield return DegradedCount;
        yield return UnhealthyCount;
        yield return UnknownCount;
    }
}

/// <summary>
/// Value object representing a summary of a single stack's health.
/// Lighter-weight than HealthSnapshot, used for list views.
/// </summary>
public sealed class StackHealthSummary : ValueObject
{
    public HealthSnapshotId SnapshotId { get; }
    public DeploymentId DeploymentId { get; }
    public string StackName { get; }
    public string? CurrentVersion { get; }
    public HealthStatus OverallStatus { get; }
    public OperationMode OperationMode { get; }
    public int HealthyServices { get; }
    public int TotalServices { get; }
    public DateTime CapturedAtUtc { get; }
    public string StatusMessage { get; }
    public bool RequiresAttention { get; }

    private StackHealthSummary(
        HealthSnapshotId snapshotId,
        DeploymentId deploymentId,
        string stackName,
        string? currentVersion,
        HealthStatus overallStatus,
        OperationMode operationMode,
        int healthyServices,
        int totalServices,
        DateTime capturedAtUtc,
        string statusMessage,
        bool requiresAttention)
    {
        SnapshotId = snapshotId;
        DeploymentId = deploymentId;
        StackName = stackName;
        CurrentVersion = currentVersion;
        OverallStatus = overallStatus;
        OperationMode = operationMode;
        HealthyServices = healthyServices;
        TotalServices = totalServices;
        CapturedAtUtc = capturedAtUtc;
        StatusMessage = statusMessage;
        RequiresAttention = requiresAttention;
    }

    /// <summary>
    /// Creates a stack health summary from a health snapshot.
    /// </summary>
    public static StackHealthSummary FromSnapshot(HealthSnapshot snapshot)
    {
        return new StackHealthSummary(
            snapshot.Id,
            snapshot.DeploymentId,
            snapshot.StackName,
            snapshot.CurrentVersion,
            snapshot.Overall,
            snapshot.OperationMode,
            snapshot.Self.HealthyCount,
            snapshot.Self.TotalCount,
            snapshot.CapturedAtUtc,
            snapshot.GetStatusMessage(),
            snapshot.RequiresAttention);
    }

    /// <summary>
    /// Gets the age of the underlying snapshot.
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - CapturedAtUtc;

    /// <summary>
    /// Gets the service health ratio as a string (e.g., "3/5").
    /// </summary>
    public string ServiceHealthRatio => $"{HealthyServices}/{TotalServices}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SnapshotId;
        yield return StackName;
        yield return CurrentVersion;
        yield return OverallStatus;
        yield return OperationMode;
        yield return HealthyServices;
        yield return TotalServices;
        yield return CapturedAtUtc;
    }
}
