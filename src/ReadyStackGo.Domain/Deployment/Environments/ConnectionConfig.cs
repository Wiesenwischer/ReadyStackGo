namespace ReadyStackGo.Domain.Deployment.Environments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Polymorphic value object representing connection configuration for an environment.
/// Subtypes: DockerSocketConfig, SshTunnelConfig, DockerTcpConfig (future).
/// </summary>
public abstract class ConnectionConfig : ValueObject
{
    /// <summary>
    /// Discriminator for JSON serialization.
    /// </summary>
    public abstract string ConfigType { get; }

    /// <summary>
    /// Returns the effective Docker host URI for Docker.DotNet client creation.
    /// For socket configs, this is the socket path.
    /// For SSH tunnels, this is resolved at runtime via SshTunnelManager.
    /// </summary>
    public abstract string GetDockerHost();

    public override string ToString() => GetDockerHost();
}

/// <summary>
/// Connection via local Docker socket (unix:// or npipe://).
/// </summary>
public sealed class DockerSocketConfig : ConnectionConfig
{
    public string SocketPath { get; }

    public override string ConfigType => "DockerSocket";

    // For JSON deserialization
    private DockerSocketConfig()
    {
        SocketPath = string.Empty;
    }

    private DockerSocketConfig(string socketPath)
    {
        SelfAssertArgumentNotEmpty(socketPath, "Socket path is required.");
        SocketPath = socketPath;
    }

    public static DockerSocketConfig Create(string path)
    {
        return new DockerSocketConfig(path);
    }

    public static DockerSocketConfig DefaultForOs()
    {
        var defaultPath = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
        return new DockerSocketConfig(defaultPath);
    }

    public override string GetDockerHost() => SocketPath;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ConfigType;
        yield return SocketPath;
    }
}

/// <summary>
/// Connection via SSH tunnel to a remote Docker socket.
/// The tunnel is managed by SshTunnelManager at runtime.
/// </summary>
public sealed class SshTunnelConfig : ConnectionConfig
{
    public string Host { get; }
    public int Port { get; }
    public string Username { get; }
    public SshAuthMethod AuthMethod { get; }
    public string RemoteSocketPath { get; }

    public override string ConfigType => "SshTunnel";

    // For JSON deserialization
    private SshTunnelConfig()
    {
        Host = string.Empty;
        Username = string.Empty;
        RemoteSocketPath = string.Empty;
    }

    private SshTunnelConfig(string host, int port, string username, SshAuthMethod authMethod, string remoteSocketPath)
    {
        SelfAssertArgumentNotEmpty(host, "SSH host is required.");
        SelfAssertArgumentRange(port, 1, 65535, "SSH port must be between 1 and 65535.");
        SelfAssertArgumentNotEmpty(username, "SSH username is required.");
        SelfAssertArgumentNotEmpty(remoteSocketPath, "Remote socket path is required.");

        Host = host;
        Port = port;
        Username = username;
        AuthMethod = authMethod;
        RemoteSocketPath = remoteSocketPath;
    }

    public static SshTunnelConfig Create(
        string host,
        int port,
        string username,
        SshAuthMethod authMethod,
        string remoteSocketPath = "/var/run/docker.sock")
    {
        return new SshTunnelConfig(host, port, username, authMethod, remoteSocketPath);
    }

    /// <summary>
    /// SSH tunnel docker host is resolved at runtime. Returns a placeholder.
    /// </summary>
    public override string GetDockerHost() => $"ssh://{Username}@{Host}:{Port}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ConfigType;
        yield return Host;
        yield return Port;
        yield return Username;
        yield return AuthMethod;
        yield return RemoteSocketPath;
    }
}

/// <summary>
/// SSH authentication method.
/// </summary>
public enum SshAuthMethod
{
    /// <summary>
    /// Authenticate with a private key.
    /// </summary>
    PrivateKey = 0,

    /// <summary>
    /// Authenticate with a password.
    /// </summary>
    Password = 1
}
