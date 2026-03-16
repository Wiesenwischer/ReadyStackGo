using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Infrastructure.Docker;

/// <summary>
/// Adapter between ISshTunnelManager and ISshConnectionTester.
/// Bridges the Infrastructure SSH tunnel result to the Application-layer TestConnectionResult.
/// </summary>
public class SshConnectionTester : ISshConnectionTester
{
    private readonly ISshTunnelManager _sshTunnelManager;

    public SshConnectionTester(ISshTunnelManager sshTunnelManager)
    {
        _sshTunnelManager = sshTunnelManager;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(
        string host,
        int port,
        string username,
        string secret,
        SshAuthMethod authMethod,
        string remoteSocketPath,
        CancellationToken cancellationToken = default)
    {
        var result = await _sshTunnelManager.TestConnectionAsync(
            host, port, username, secret, authMethod, remoteSocketPath, cancellationToken);

        return new TestConnectionResult(result.Success, result.Message, result.DockerVersion);
    }
}
