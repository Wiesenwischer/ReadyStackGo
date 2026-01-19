namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Represents a point-in-time snapshot of a deployment's state.
/// Used for rollback functionality to restore previous configurations.
/// </summary>
public class DeploymentSnapshot : Entity<DeploymentSnapshotId>
{
    public DeploymentId DeploymentId { get; private set; } = null!;
    public string StackVersion { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public string? Description { get; private set; }

    private readonly Dictionary<string, string> _variables = new();
    public IReadOnlyDictionary<string, string> Variables => _variables;

    private readonly List<ServiceSnapshot> _services = new();
    public IReadOnlyCollection<ServiceSnapshot> Services => _services.AsReadOnly();

    // For EF Core
    protected DeploymentSnapshot() { }

    private DeploymentSnapshot(
        DeploymentSnapshotId id,
        DeploymentId deploymentId,
        string stackVersion,
        IReadOnlyDictionary<string, string> variables,
        IEnumerable<ServiceSnapshot> services,
        string? description)
    {
        Id = id;
        DeploymentId = deploymentId;
        StackVersion = stackVersion;
        CreatedAt = SystemClock.UtcNow;
        Description = description;

        foreach (var (key, value) in variables)
            _variables[key] = value;

        _services.AddRange(services);
    }

    /// <summary>
    /// Creates a new deployment snapshot.
    /// </summary>
    public static DeploymentSnapshot Create(
        DeploymentSnapshotId id,
        DeploymentId deploymentId,
        string stackVersion,
        IReadOnlyDictionary<string, string> variables,
        IEnumerable<ServiceSnapshot> services,
        string? description = null)
    {
        return new DeploymentSnapshot(id, deploymentId, stackVersion, variables, services, description);
    }
}

/// <summary>
/// Represents a snapshot of a service's state at a point in time.
/// </summary>
public record ServiceSnapshot
{
    /// <summary>
    /// Name of the service.
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// Docker image including tag (e.g., "myapp/api:1.5.0").
    /// </summary>
    public string Image { get; init; } = null!;

    // For EF Core / JSON deserialization
    public ServiceSnapshot() { }

    public ServiceSnapshot(string name, string image)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }
}
