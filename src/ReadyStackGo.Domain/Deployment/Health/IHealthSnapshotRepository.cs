namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Repository interface for health snapshots.
/// </summary>
public interface IHealthSnapshotRepository
{
    /// <summary>
    /// Generates a new unique identity.
    /// </summary>
    HealthSnapshotId NextIdentity();

    /// <summary>
    /// Adds a new health snapshot.
    /// </summary>
    void Add(HealthSnapshot snapshot);

    /// <summary>
    /// Gets a health snapshot by ID.
    /// </summary>
    HealthSnapshot? Get(HealthSnapshotId id);

    /// <summary>
    /// Gets the latest health snapshot for a deployment.
    /// </summary>
    HealthSnapshot? GetLatestForDeployment(DeploymentId deploymentId);

    /// <summary>
    /// Gets all health snapshots for an environment.
    /// </summary>
    IEnumerable<HealthSnapshot> GetLatestForEnvironment(EnvironmentId environmentId);

    /// <summary>
    /// Gets health history for a deployment (newest first).
    /// </summary>
    IEnumerable<HealthSnapshot> GetHistory(DeploymentId deploymentId, int limit = 10);

    /// <summary>
    /// Removes old snapshots older than the specified age.
    /// </summary>
    void RemoveOlderThan(TimeSpan age);

    /// <summary>
    /// Saves changes to the repository.
    /// </summary>
    void SaveChanges();
}
