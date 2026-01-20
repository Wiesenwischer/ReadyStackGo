namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// A point-in-time snapshot of health status for a stack deployment.
/// Aggregate root for health monitoring.
/// </summary>
public sealed class HealthSnapshot : AggregateRoot<HealthSnapshotId>
{
    /// <summary>
    /// Organization this snapshot belongs to.
    /// </summary>
    public OrganizationId OrganizationId { get; private set; }

    /// <summary>
    /// Environment this snapshot belongs to.
    /// </summary>
    public EnvironmentId EnvironmentId { get; private set; }

    /// <summary>
    /// Deployment this snapshot is for.
    /// </summary>
    public DeploymentId DeploymentId { get; private set; }

    /// <summary>
    /// Stack name for quick reference.
    /// </summary>
    public string StackName { get; private set; }

    /// <summary>
    /// Timestamp when this snapshot was captured.
    /// </summary>
    public DateTime CapturedAtUtc { get; private set; }

    /// <summary>
    /// Overall aggregated health status.
    /// </summary>
    public HealthStatus Overall { get; private set; }

    /// <summary>
    /// Current operation mode (controlled by RSGO).
    /// </summary>
    public OperationMode OperationMode { get; private set; }

    /// <summary>
    /// Target version during upgrade/migration.
    /// </summary>
    public string? TargetVersion { get; private set; }

    /// <summary>
    /// Current deployed version.
    /// </summary>
    public string? CurrentVersion { get; private set; }

    /// <summary>
    /// NServiceBus/messaging health (optional, for NSB-based stacks).
    /// </summary>
    public BusHealth? Bus { get; private set; }

    /// <summary>
    /// Infrastructure health (databases, disks, external services).
    /// </summary>
    public InfraHealth? Infra { get; private set; }

    /// <summary>
    /// Health of stack-controlled services/containers.
    /// </summary>
    public SelfHealth Self { get; private set; }

    // For EF Core
    private HealthSnapshot()
    {
        Id = HealthSnapshotId.Create();
        OrganizationId = null!;
        EnvironmentId = null!;
        DeploymentId = null!;
        StackName = string.Empty;
        Overall = HealthStatus.Unknown;
        OperationMode = OperationMode.Normal;
        Self = SelfHealth.Empty();
    }

    private HealthSnapshot(
        HealthSnapshotId id,
        OrganizationId organizationId,
        EnvironmentId environmentId,
        DeploymentId deploymentId,
        string stackName,
        OperationMode operationMode,
        string? currentVersion,
        string? targetVersion,
        BusHealth? bus,
        InfraHealth? infra,
        SelfHealth self)
    {
        SelfAssertArgumentNotNull(organizationId, "OrganizationId cannot be null.");
        SelfAssertArgumentNotNull(environmentId, "EnvironmentId cannot be null.");
        SelfAssertArgumentNotNull(deploymentId, "DeploymentId cannot be null.");
        SelfAssertArgumentNotEmpty(stackName, "StackName cannot be empty.");

        Id = id;
        OrganizationId = organizationId;
        EnvironmentId = environmentId;
        DeploymentId = deploymentId;
        StackName = stackName;
        CapturedAtUtc = SystemClock.UtcNow;
        OperationMode = operationMode;
        CurrentVersion = currentVersion;
        TargetVersion = targetVersion;
        Bus = bus;
        Infra = infra;
        Self = self ?? SelfHealth.Empty();

        Overall = CalculateOverallStatus();
    }

    /// <summary>
    /// Creates a new health snapshot for a deployment.
    /// </summary>
    public static HealthSnapshot Capture(
        OrganizationId organizationId,
        EnvironmentId environmentId,
        DeploymentId deploymentId,
        string stackName,
        OperationMode operationMode,
        string? currentVersion = null,
        string? targetVersion = null,
        BusHealth? bus = null,
        InfraHealth? infra = null,
        SelfHealth? self = null)
    {
        var snapshot = new HealthSnapshot(
            HealthSnapshotId.Create(),
            organizationId,
            environmentId,
            deploymentId,
            stackName,
            operationMode,
            currentVersion,
            targetVersion,
            bus,
            infra,
            self ?? SelfHealth.Empty());

        snapshot.AddDomainEvent(new HealthSnapshotCaptured(
            snapshot.Id,
            snapshot.DeploymentId,
            snapshot.Overall,
            snapshot.OperationMode));

        return snapshot;
    }

    /// <summary>
    /// Calculates the overall health status based on operation mode and component health.
    /// </summary>
    private HealthStatus CalculateOverallStatus()
    {
        // If operation mode expects degraded health, start from there
        var minimumStatus = OperationMode.MinimumHealthStatus;

        // Collect all component statuses
        var componentStatuses = new List<HealthStatus> { Self.Status };

        if (Bus != null)
            componentStatuses.Add(Bus.Status);

        if (Infra != null)
            componentStatuses.Add(Infra.Status);

        // Get the worst component status
        var worstComponentStatus = HealthStatus.Aggregate(componentStatuses);

        // Overall is the worse of minimum (from operation mode) and actual component health
        return minimumStatus.CombineWith(worstComponentStatus);
    }

    /// <summary>
    /// Indicates if this snapshot shows a healthy state.
    /// </summary>
    public bool IsHealthy => Overall == HealthStatus.Healthy && OperationMode == OperationMode.Normal;

    /// <summary>
    /// Indicates if this snapshot requires attention.
    /// </summary>
    public bool RequiresAttention => Overall.RequiresAttention;

    /// <summary>
    /// Gets a human-readable status message.
    /// </summary>
    public string GetStatusMessage()
    {
        if (OperationMode == OperationMode.Maintenance)
            return "Maintenance mode";

        if (Overall == HealthStatus.Unhealthy)
            return $"Unhealthy: {Self.TotalCount - Self.HealthyCount} service(s) down";

        if (Overall == HealthStatus.Degraded)
            return "Some services degraded";

        return "All systems operational";
    }

    /// <summary>
    /// Gets the age of this snapshot.
    /// </summary>
    public TimeSpan Age => SystemClock.UtcNow - CapturedAtUtc;

    /// <summary>
    /// Indicates if this snapshot is stale (older than threshold).
    /// </summary>
    public bool IsStale(TimeSpan threshold) => Age > threshold;
}

/// <summary>
/// Domain event raised when a health snapshot is captured.
/// </summary>
public sealed class HealthSnapshotCaptured : DomainEvent
{
    public HealthSnapshotId SnapshotId { get; }
    public DeploymentId DeploymentId { get; }
    public HealthStatus OverallStatus { get; }
    public OperationMode OperationMode { get; }

    public HealthSnapshotCaptured(
        HealthSnapshotId snapshotId,
        DeploymentId deploymentId,
        HealthStatus overallStatus,
        OperationMode operationMode)
    {
        SnapshotId = snapshotId;
        DeploymentId = deploymentId;
        OverallStatus = overallStatus;
        OperationMode = operationMode;
    }
}
