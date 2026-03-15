namespace ReadyStackGo.Domain.Deployment.Environments;

using ReadyStackGo.Domain.SharedKernel;

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
    public SshCredential? SshCredential { get; private set; }
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
        CreatedAt = SystemClock.UtcNow;

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
        var connectionConfig = DockerSocketConfig.Create(socketPath);
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
        var connectionConfig = DockerSocketConfig.DefaultForOs();
        return new Environment(id, organizationId, name, description, EnvironmentType.DockerSocket, connectionConfig);
    }

    /// <summary>
    /// Creates a new SSH Tunnel environment.
    /// </summary>
    public static Environment CreateSshTunnel(
        EnvironmentId id,
        OrganizationId organizationId,
        string name,
        string? description,
        SshTunnelConfig sshConfig,
        SshCredential sshCredential)
    {
        ArgumentNullException.ThrowIfNull(sshConfig, nameof(sshConfig));
        ArgumentNullException.ThrowIfNull(sshCredential, nameof(sshCredential));
        var env = new Environment(id, organizationId, name, description, EnvironmentType.SshTunnel, sshConfig);
        env.SshCredential = sshCredential;
        return env;
    }

    /// <summary>
    /// Sets this environment as the default for the organization.
    /// </summary>
    public void SetAsDefault()
    {
        IsDefault = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Unsets this environment as the default.
    /// </summary>
    public void UnsetAsDefault()
    {
        IsDefault = false;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates the environment name.
    /// </summary>
    public void UpdateName(string name)
    {
        SelfAssertArgumentNotEmpty(name, "Environment name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Environment name must be 100 characters or less.");

        Name = name;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates the connection configuration.
    /// </summary>
    public void UpdateConnectionConfig(ConnectionConfig config)
    {
        SelfAssertArgumentNotNull(config, "ConnectionConfig is required.");

        ConnectionConfig = config;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates the SSH credential. Only valid for SSH tunnel environments.
    /// </summary>
    public void UpdateSshCredential(SshCredential credential)
    {
        SelfAssertArgumentNotNull(credential, "SSH credential is required.");
        if (Type != EnvironmentType.SshTunnel)
            throw new InvalidOperationException("SSH credentials can only be set on SSH tunnel environments.");

        SshCredential = credential;
        UpdatedAt = SystemClock.UtcNow;
    }

    public override string ToString() =>
        $"Environment [id={Id}, name={Name}, type={Type}, isDefault={IsDefault}]";
}
