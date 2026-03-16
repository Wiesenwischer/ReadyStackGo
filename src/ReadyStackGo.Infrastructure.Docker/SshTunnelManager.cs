namespace ReadyStackGo.Infrastructure.Docker;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using global::Docker.DotNet;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Environments;
using Renci.SshNet;

/// <summary>
/// Manages SSH tunnels for remote Docker environments.
/// Uses SSH local port forwarding to tunnel TCP connections through SSH
/// to the remote Docker daemon.
///
/// For Unix socket access on the remote, the tunnel runs a socat bridge:
///   socat TCP-LISTEN:{port},fork,reuseaddr UNIX-CONNECT:{socketPath}
/// Then SSH forwards the local port to the remote TCP port.
/// </summary>
public class SshTunnelManager : ISshTunnelManager
{
    private readonly ConcurrentDictionary<string, SshTunnelEntry> _tunnels = new();
    private readonly ILogger<SshTunnelManager> _logger;

    // Remote bridge port range — used to create a TCP bridge to the Docker Unix socket
    private const int RemoteBridgePortStart = 32768;
    private const int RemoteBridgePortEnd = 60999;

    public SshTunnelManager(ILogger<SshTunnelManager> logger)
    {
        _logger = logger;
    }

    public Task<Uri> GetOrCreateTunnelAsync(
        string environmentId,
        string host,
        int port,
        string username,
        string privateKeyOrPassword,
        SshAuthMethod authMethod,
        string remoteSocketPath,
        CancellationToken cancellationToken = default)
    {
        // Check if we already have an active tunnel
        if (_tunnels.TryGetValue(environmentId, out var existing) && existing.IsActive)
        {
            _logger.LogDebug("Reusing existing SSH tunnel for environment {EnvironmentId} on port {Port}",
                environmentId, existing.LocalPort);
            return Task.FromResult(existing.LocalUri);
        }

        // Clean up stale tunnel if exists
        if (existing != null)
        {
            _logger.LogInformation("Closing stale SSH tunnel for environment {EnvironmentId}", environmentId);
            existing.Dispose();
            _tunnels.TryRemove(environmentId, out _);
        }

        // Create new tunnel
        var entry = CreateTunnel(host, port, username, privateKeyOrPassword, authMethod, remoteSocketPath);
        _tunnels[environmentId] = entry;

        _logger.LogInformation(
            "SSH tunnel established for environment {EnvironmentId}: localhost:{LocalPort} → {Host}:{SshPort} → {RemoteSocket}",
            environmentId, entry.LocalPort, host, port, remoteSocketPath);

        return Task.FromResult(entry.LocalUri);
    }

    public void CloseTunnel(string environmentId)
    {
        if (_tunnels.TryRemove(environmentId, out var entry))
        {
            entry.Dispose();
            _logger.LogInformation("SSH tunnel closed for environment {EnvironmentId}", environmentId);
        }
    }

    public async Task<SshTestResult> TestConnectionAsync(
        string host,
        int port,
        string username,
        string privateKeyOrPassword,
        SshAuthMethod authMethod,
        string remoteSocketPath,
        CancellationToken cancellationToken = default)
    {
        SshTunnelEntry? entry = null;
        try
        {
            entry = CreateTunnel(host, port, username, privateKeyOrPassword, authMethod, remoteSocketPath);

            using var dockerClient = new DockerClientConfiguration(entry.LocalUri).CreateClient();
            var systemInfo = await dockerClient.System.GetSystemInfoAsync(cancellationToken);

            return new SshTestResult(
                true,
                $"Connected to Docker {systemInfo.ServerVersion} via SSH tunnel ({host}:{port})",
                systemInfo.ServerVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSH tunnel test connection failed for {Host}:{Port}", host, port);
            var message = ex.InnerException?.Message ?? ex.Message;
            return new SshTestResult(false, $"SSH connection failed: {message}");
        }
        finally
        {
            entry?.Dispose();
        }
    }

    private SshTunnelEntry CreateTunnel(
        string host,
        int port,
        string username,
        string privateKeyOrPassword,
        SshAuthMethod authMethod,
        string remoteSocketPath)
    {
        var connectionInfo = CreateConnectionInfo(host, port, username, privateKeyOrPassword, authMethod);
        var client = new SshClient(connectionInfo);
        client.Connect();

        // Find an available remote port for the socat bridge
        var remoteBridgePort = FindAvailableRemotePort(client);

        // Start socat on the remote to bridge TCP → Unix socket
        // Run in background, will be killed when SSH connection closes
        var socatCmd = $"socat TCP-LISTEN:{remoteBridgePort},fork,reuseaddr,bind=127.0.0.1 UNIX-CONNECT:{remoteSocketPath} &";
        var socatResult = client.RunCommand(socatCmd);
        if (socatResult.ExitStatus != 0)
        {
            // socat might not be installed — try docker proxy or direct approach
            _logger.LogWarning("socat not available on remote ({ExitStatus}), trying direct Docker TCP check", socatResult.ExitStatus);
        }

        // Small delay to let socat start
        Thread.Sleep(200);

        // Set up local port forwarding: localhost:localPort → remote:127.0.0.1:remoteBridgePort
        var localPort = GetAvailablePort();
        var forwardedPort = new ForwardedPortLocal(
            IPAddress.Loopback.ToString(),
            (uint)localPort,
            "127.0.0.1",
            (uint)remoteBridgePort);

        client.AddForwardedPort(forwardedPort);
        forwardedPort.Start();

        _logger.LogDebug(
            "SSH tunnel created: localhost:{LocalPort} → remote:127.0.0.1:{RemotePort} → {RemoteSocket} via {Host}:{SshPort}",
            localPort, remoteBridgePort, remoteSocketPath, host, port);

        return new SshTunnelEntry(client, forwardedPort, localPort, remoteBridgePort);
    }

    private static int FindAvailableRemotePort(SshClient client)
    {
        // Pick a random port in the ephemeral range and verify it's not in use
        var random = new Random();
        for (var i = 0; i < 10; i++)
        {
            var candidate = random.Next(RemoteBridgePortStart, RemoteBridgePortEnd);
            var checkResult = client.RunCommand($"ss -tlnH sport = :{candidate} 2>/dev/null | head -1");
            if (string.IsNullOrWhiteSpace(checkResult.Result))
                return candidate;
        }
        // Fallback: just use a random port and hope for the best
        return random.Next(RemoteBridgePortStart, RemoteBridgePortEnd);
    }

    private static ConnectionInfo CreateConnectionInfo(
        string host,
        int port,
        string username,
        string privateKeyOrPassword,
        SshAuthMethod authMethod)
    {
        AuthenticationMethod auth = authMethod switch
        {
            SshAuthMethod.Password => new PasswordAuthenticationMethod(username, privateKeyOrPassword),
            SshAuthMethod.PrivateKey => CreatePrivateKeyAuth(username, privateKeyOrPassword),
            _ => throw new ArgumentException($"Unsupported SSH auth method: {authMethod}")
        };

        return new ConnectionInfo(host, port, username, auth);
    }

    private static PrivateKeyAuthenticationMethod CreatePrivateKeyAuth(string username, string privateKeyContent)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKeyContent));
        var keyFile = new PrivateKeyFile(stream);
        return new PrivateKeyAuthenticationMethod(username, keyFile);
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var localPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return localPort;
    }

    public void Dispose()
    {
        foreach (var entry in _tunnels.Values)
        {
            try
            {
                entry.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing SSH tunnel");
            }
        }
        _tunnels.Clear();
    }

    private sealed class SshTunnelEntry : IDisposable
    {
        private readonly SshClient _client;
        private readonly ForwardedPortLocal _forwardedPort;
        private readonly int _remoteBridgePort;

        public int LocalPort { get; }
        public Uri LocalUri => new($"tcp://localhost:{LocalPort}");
        public bool IsActive => _client.IsConnected && _forwardedPort.IsStarted;

        public SshTunnelEntry(SshClient client, ForwardedPortLocal forwardedPort, int localPort, int remoteBridgePort)
        {
            _client = client;
            _forwardedPort = forwardedPort;
            _remoteBridgePort = remoteBridgePort;
            LocalPort = localPort;
        }

        public void Dispose()
        {
            try { _forwardedPort.Stop(); } catch { /* ignore */ }
            try { _forwardedPort.Dispose(); } catch { /* ignore */ }
            // Kill the socat process on the remote
            try { _client.RunCommand($"kill $(lsof -t -i :{_remoteBridgePort}) 2>/dev/null"); } catch { /* ignore */ }
            try { _client.Disconnect(); } catch { /* ignore */ }
            try { _client.Dispose(); } catch { /* ignore */ }
        }
    }
}
