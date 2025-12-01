namespace ReadyStackGo.Domain.Deployment.Aggregates;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;
using ReadyStackGo.Domain.Deployment.Events;
using ReadyStackGo.Domain.Deployment.ValueObjects;

/// <summary>
/// Aggregate root representing a stack deployment to an environment.
/// </summary>
public class Deployment : AggregateRoot<DeploymentId>
{
    public EnvironmentId EnvironmentId { get; private set; } = null!;
    public string StackName { get; private set; } = null!;
    public string? StackVersion { get; private set; }
    public string ProjectName { get; private set; } = null!;
    public DeploymentStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public UserId DeployedBy { get; private set; } = null!;

    private readonly List<DeployedService> _services = new();
    public IReadOnlyCollection<DeployedService> Services => _services.AsReadOnly();

    // For EF Core
    protected Deployment() { }

    private Deployment(
        DeploymentId id,
        EnvironmentId environmentId,
        string stackName,
        string projectName,
        UserId deployedBy)
    {
        SelfAssertArgumentNotNull(id, "DeploymentId is required.");
        SelfAssertArgumentNotNull(environmentId, "EnvironmentId is required.");
        SelfAssertArgumentNotEmpty(stackName, "Stack name is required.");
        SelfAssertArgumentNotEmpty(projectName, "Project name is required.");
        SelfAssertArgumentNotNull(deployedBy, "DeployedBy is required.");

        Id = id;
        EnvironmentId = environmentId;
        StackName = stackName;
        ProjectName = projectName;
        DeployedBy = deployedBy;
        Status = DeploymentStatus.Pending;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new DeploymentStarted(Id, EnvironmentId, StackName));
    }

    /// <summary>
    /// Starts a new deployment.
    /// </summary>
    public static Deployment Start(
        DeploymentId id,
        EnvironmentId environmentId,
        string stackName,
        string projectName,
        UserId deployedBy)
    {
        return new Deployment(id, environmentId, stackName, projectName, deployedBy);
    }

    /// <summary>
    /// Sets the stack version.
    /// </summary>
    public void SetStackVersion(string version)
    {
        StackVersion = version;
    }

    /// <summary>
    /// Marks the deployment as running with deployed services.
    /// </summary>
    public void MarkAsRunning(IEnumerable<DeployedService> services)
    {
        SelfAssertArgumentTrue(Status == DeploymentStatus.Pending,
            "Can only mark as running from Pending state.");

        Status = DeploymentStatus.Running;
        CompletedAt = DateTime.UtcNow;

        _services.Clear();
        _services.AddRange(services);

        AddDomainEvent(new DeploymentCompleted(Id, Status));
    }

    /// <summary>
    /// Marks the deployment as failed.
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        Status = DeploymentStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new DeploymentCompleted(Id, Status, errorMessage));
    }

    /// <summary>
    /// Marks the deployment as stopped.
    /// </summary>
    public void MarkAsStopped()
    {
        SelfAssertArgumentTrue(Status == DeploymentStatus.Running,
            "Can only stop a running deployment.");

        Status = DeploymentStatus.Stopped;

        foreach (var service in _services)
        {
            service.UpdateStatus("stopped");
        }
    }

    /// <summary>
    /// Marks the deployment as removed.
    /// </summary>
    public void MarkAsRemoved()
    {
        Status = DeploymentStatus.Removed;

        foreach (var service in _services)
        {
            service.UpdateStatus("removed");
        }
    }

    /// <summary>
    /// Updates the status of a specific service.
    /// </summary>
    public void UpdateServiceStatus(string serviceName, string status)
    {
        var service = _services.FirstOrDefault(s => s.ServiceName == serviceName);
        service?.UpdateStatus(status);
    }

    public override string ToString() =>
        $"Deployment [id={Id}, stack={StackName}, status={Status}]";
}
