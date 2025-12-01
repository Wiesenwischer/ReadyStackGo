namespace ReadyStackGo.Domain.Deployment.Aggregates;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;
using ReadyStackGo.Domain.Deployment.Events;
using ReadyStackGo.Domain.Deployment.ValueObjects;

/// <summary>
/// Aggregate root representing a Docker environment where stacks can be deployed.
/// </summary>
public class Environment : AggregateRoot<EnvironmentId>
{
    public OrganizationId OrganizationId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public EnvironmentType Type { get; private set; }
    public ConnectionConfig ConnectionConfig { get; private set; } = null!;
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // For EF Core
    protected Environment() { }

    private Environment(
        EnvironmentId id,
        OrganizationId organizationId,
        string name,
        string? description,
        EnvironmentType type,
        ConnectionConfig connectionConfig)
    {
        SelfAssertArgumentNotNull(id, "EnvironmentId is required.");
        SelfAssertArgumentNotNull(organizationId, "OrganizationId is required.");
        SelfAssertArgumentNotEmpty(name, "Environment name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Environment name must be 100 characters or less.");
        SelfAssertArgumentNotNull(connectionConfig, "ConnectionConfig is required.");

        Id = id;
        OrganizationId = organizationId;
        Name = name;
        Description = description;
        Type = type;
        ConnectionConfig = connectionConfig;
        IsDefault = false;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new EnvironmentCreated(Id, Name));
    }

    /// <summary>
    /// Creates a new Docker Socket environment.
    /// </summary>
    public static Environment CreateDockerSocket(
        EnvironmentId id,
        OrganizationId organizationId,
        string name,
        string? description,
        string socketPath)
    {
        var connectionConfig = ConnectionConfig.DockerSocket(socketPath);
        return new Environment(id, organizationId, name, description, EnvironmentType.DockerSocket, connectionConfig);
    }

    /// <summary>
    /// Creates a new environment with default Docker socket path.
    /// </summary>
    public static Environment CreateDefault(
        EnvironmentId id,
        OrganizationId organizationId,
        string name,
        string? description = null)
    {
        var connectionConfig = ConnectionConfig.DefaultDockerSocket();
        return new Environment(id, organizationId, name, description, EnvironmentType.DockerSocket, connectionConfig);
    }

    /// <summary>
    /// Sets this environment as the default for the organization.
    /// </summary>
    public void SetAsDefault()
    {
        IsDefault = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Unsets this environment as the default.
    /// </summary>
    public void UnsetAsDefault()
    {
        IsDefault = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the environment name.
    /// </summary>
    public void UpdateName(string name)
    {
        SelfAssertArgumentNotEmpty(name, "Environment name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Environment name must be 100 characters or less.");

        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the connection configuration.
    /// </summary>
    public void UpdateConnectionConfig(ConnectionConfig config)
    {
        SelfAssertArgumentNotNull(config, "ConnectionConfig is required.");

        ConnectionConfig = config;
        UpdatedAt = DateTime.UtcNow;
    }

    public override string ToString() =>
        $"Environment [id={Id}, name={Name}, type={Type}, isDefault={IsDefault}]";
}
