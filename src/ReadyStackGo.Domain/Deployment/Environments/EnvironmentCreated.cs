namespace ReadyStackGo.Domain.Deployment.Environments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Event raised when an environment is created.
/// </summary>
public sealed class EnvironmentCreated : DomainEvent
{
    public EnvironmentId EnvironmentId { get; }
    public string Name { get; }

    public EnvironmentCreated(EnvironmentId environmentId, string name)
    {
        EnvironmentId = environmentId;
        Name = name;
    }
}
