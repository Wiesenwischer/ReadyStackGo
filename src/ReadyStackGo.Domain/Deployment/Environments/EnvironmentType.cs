namespace ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Type of Docker environment connection.
/// </summary>
public enum EnvironmentType
{
    /// <summary>
    /// Local Docker socket connection.
    /// </summary>
    DockerSocket = 0,

    /// <summary>
    /// Remote Docker API connection via TCP/TLS (future).
    /// </summary>
    DockerTcp = 1,

    /// <summary>
    /// Remote Docker agent connection (future).
    /// </summary>
    DockerAgent = 2,

    /// <summary>
    /// Remote Docker connection via SSH tunnel.
    /// </summary>
    SshTunnel = 3
}
