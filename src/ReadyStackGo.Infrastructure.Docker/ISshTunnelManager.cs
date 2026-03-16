namespace ReadyStackGo.Infrastructure.Docker;

/// <summary>
/// Manages SSH tunnels for remote Docker environments.
/// Tunnels are created lazily on first access and cached per environment.
/// </summary>
public interface ISshTunnelManager : IDisposable
{
    /// <summary>
    /// Gets or creates an SSH tunnel for the given environment and returns the local Docker URI.
    /// The tunnel forwards localhost:random-port to the remote Docker socket.
    /// </summary>
    /// <param name="environmentId">The environment ID to tunnel for.</param>
    /// <param name="host">SSH host.</param>
    /// <param name="port">SSH port.</param>
    /// <param name="username">SSH username.</param>
    /// <param name="privateKeyOrPassword">Decrypted private key content or password.</param>
    /// <param name="authMethod">Authentication method (PrivateKey or Password).</param>
    /// <param name="remoteSocketPath">Remote Docker socket path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Local Docker URI in the form tcp://localhost:{port}.</returns>
    Task<Uri> GetOrCreateTunnelAsync(
        string environmentId,
        string host,
        int port,
        string username,
        string privateKeyOrPassword,
        Domain.Deployment.Environments.SshAuthMethod authMethod,
        string remoteSocketPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the tunnel for the given environment.
    /// </summary>
    void CloseTunnel(string environmentId);

    /// <summary>
    /// Tests an SSH connection and returns Docker system info.
    /// Creates a temporary tunnel that is closed after the test.
    /// </summary>
    Task<SshTestResult> TestConnectionAsync(
        string host,
        int port,
        string username,
        string privateKeyOrPassword,
        Domain.Deployment.Environments.SshAuthMethod authMethod,
        string remoteSocketPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an SSH tunnel connection test.
/// </summary>
public record SshTestResult(bool Success, string Message, string? DockerVersion = null);
