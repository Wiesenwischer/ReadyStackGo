namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Repository interface for Deployment aggregate.
/// </summary>
public interface IDeploymentRepository
{
    /// <summary>
    /// Generates a new unique deployment identity.
    /// </summary>
    DeploymentId NextIdentity();

    /// <summary>
    /// Adds a new deployment.
    /// </summary>
    void Add(Deployment deployment);

    /// <summary>
    /// Updates an existing deployment.
    /// </summary>
    void Update(Deployment deployment);

    /// <summary>
    /// Gets a deployment by its ID.
    /// </summary>
    Deployment? Get(DeploymentId id);

    /// <summary>
    /// Gets a deployment by its ID (alias for Get).
    /// </summary>
    Deployment? GetById(DeploymentId id);

    /// <summary>
    /// Gets all deployments for an environment.
    /// </summary>
    IEnumerable<Deployment> GetByEnvironment(EnvironmentId environmentId);

    /// <summary>
    /// Gets a deployment by stack name within an environment.
    /// </summary>
    Deployment? GetByStackName(EnvironmentId environmentId, string stackName);

    /// <summary>
    /// Removes a deployment.
    /// </summary>
    void Remove(Deployment deployment);

    /// <summary>
    /// Gets all active deployments (Running or Stopped status).
    /// </summary>
    IEnumerable<Deployment> GetAllActive();

    /// <summary>
    /// Gets all deployments with the specified status.
    /// </summary>
    IEnumerable<Deployment> GetByStatus(DeploymentStatus status);

    /// <summary>
    /// Gets all deployments with any of the specified statuses.
    /// </summary>
    IEnumerable<Deployment> GetByStatuses(params DeploymentStatus[] statuses);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    void SaveChanges();
}
