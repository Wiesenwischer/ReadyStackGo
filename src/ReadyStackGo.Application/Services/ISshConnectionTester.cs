using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Tests SSH tunnel connections to remote Docker daemons.
/// Application-layer abstraction over ISshTunnelManager.
/// </summary>
public interface ISshConnectionTester
{
    Task<TestConnectionResult> TestConnectionAsync(
        string host,
        int port,
        string username,
        string secret,
        SshAuthMethod authMethod,
        string remoteSocketPath,
        CancellationToken cancellationToken = default);
}
