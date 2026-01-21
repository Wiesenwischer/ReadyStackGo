namespace ReadyStackGo.Domain.Deployment.Registries;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Event raised when a new registry is created.
/// </summary>
public sealed class RegistryCreated : DomainEvent
{
    public RegistryId RegistryId { get; }
    public string Name { get; }
    public string Url { get; }

    public RegistryCreated(RegistryId registryId, string name, string url)
    {
        RegistryId = registryId;
        Name = name;
        Url = url;
    }
}

/// <summary>
/// Event raised when a registry is updated.
/// </summary>
public sealed class RegistryUpdated : DomainEvent
{
    public RegistryId RegistryId { get; }
    public string Name { get; }

    public RegistryUpdated(RegistryId registryId, string name)
    {
        RegistryId = registryId;
        Name = name;
    }
}

/// <summary>
/// Event raised when a registry is deleted.
/// </summary>
public sealed class RegistryDeleted : DomainEvent
{
    public RegistryId RegistryId { get; }
    public string Name { get; }

    public RegistryDeleted(RegistryId registryId, string name)
    {
        RegistryId = registryId;
        Name = name;
    }
}
