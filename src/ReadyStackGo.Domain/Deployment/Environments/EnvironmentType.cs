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
    /// Remote Docker API connection (future).
    /// </summary>
    DockerApi = 1,

    /// <summary>
    /// Remote Docker agent connection (future).
    /// </summary>
    DockerAgent = 2
}
