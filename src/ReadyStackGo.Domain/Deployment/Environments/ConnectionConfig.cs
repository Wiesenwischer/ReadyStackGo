namespace ReadyStackGo.Domain.Deployment.Environments;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object representing connection configuration for an environment.
/// </summary>
public sealed class ConnectionConfig : ValueObject
{
    public string SocketPath { get; }

    // For EF Core
    private ConnectionConfig()
    {
        SocketPath = string.Empty;
    }

    private ConnectionConfig(string socketPath)
    {
        SelfAssertArgumentNotEmpty(socketPath, "Socket path is required.");
        SocketPath = socketPath;
    }

    /// <summary>
    /// Creates a Docker socket connection configuration.
    /// </summary>
    public static ConnectionConfig DockerSocket(string path)
    {
        return new ConnectionConfig(path);
    }

    /// <summary>
    /// Creates a default Docker socket connection based on the OS.
    /// </summary>
    public static ConnectionConfig DefaultDockerSocket()
    {
        var defaultPath = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
        return new ConnectionConfig(defaultPath);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SocketPath;
    }

    public override string ToString() => SocketPath;
}
