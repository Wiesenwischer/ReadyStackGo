namespace ReadyStackGo.Domain.Organizations;

/// <summary>
/// Environment that connects to Docker via Unix socket (Linux) or named pipe (Windows).
/// This is the primary environment type for v0.4.
/// </summary>
public class DockerSocketEnvironment : Environment
{
    /// <summary>
    /// Path to the Docker socket.
    /// Linux: "unix:///var/run/docker.sock"
    /// Windows: "npipe:////./pipe/docker_engine"
    /// Can also be a remote socket: "tcp://docker-host:2375"
    /// </summary>
    public required string SocketPath { get; set; }

    /// <summary>
    /// Returns the socket path as the unique connection string.
    /// </summary>
    public override string GetConnectionString() => SocketPath;

    /// <summary>
    /// Returns "docker-socket" as the environment type.
    /// </summary>
    public override string GetEnvironmentType() => "docker-socket";

    /// <summary>
    /// Creates a default local Docker socket environment based on the current OS.
    /// </summary>
    public static DockerSocketEnvironment CreateLocal(string id, string name)
    {
        var socketPath = OperatingSystem.IsWindows()
            ? "npipe:////./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        return new DockerSocketEnvironment
        {
            Id = id,
            Name = name,
            SocketPath = socketPath,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
