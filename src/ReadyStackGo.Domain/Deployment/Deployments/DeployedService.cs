namespace ReadyStackGo.Domain.Deployment.Deployments;

/// <summary>
/// Owned entity representing a deployed service within a deployment.
/// Has no independent identity - exists only in the context of a Deployment aggregate.
/// </summary>
public class DeployedService
{
    public string ServiceName { get; private set; } = null!;
    public string? ContainerId { get; private set; }
    public string? ContainerName { get; private set; }
    public string? Image { get; private set; }
    public string Status { get; private set; } = null!;

    // For EF Core
    protected DeployedService() { }

    public DeployedService(
        string serviceName,
        string? containerId,
        string? containerName,
        string? image,
        string status)
    {
        ServiceName = serviceName;
        ContainerId = containerId;
        ContainerName = containerName;
        Image = image;
        Status = status;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void UpdateContainerInfo(string containerId, string containerName)
    {
        ContainerId = containerId;
        ContainerName = containerName;
    }
}
