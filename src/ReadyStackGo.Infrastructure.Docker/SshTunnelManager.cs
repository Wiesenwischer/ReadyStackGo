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
/// Uses a local TCP listener combined with SSH exec channels running
/// "socat STDIO UNIX-CONNECT:{socketPath}" to bridge each TCP connection
/// directly to the remote Docker Unix socket through SSH.
///
/// This avoids SSH.NET's ForwardedPortLocal which is unreliable in Docker containers.
/// </summary>
public class SshTunnelManager : ISshTunnelManager
{
    private readonly ConcurrentDictionary<string, SshTunnelEntry> _tunnels = new();
    private readonly ILogger<SshTunnelManager> _logger;

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

            // Use a timeout for the Docker API call — the tunnel might connect but Docker might not respond
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            var systemInfo = await dockerClient.System.GetSystemInfoAsync(cts.Token);

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

        // Auto-accept host keys (RSGO manages its own trust, no known_hosts file)
        client.HostKeyReceived += (_, e) =>
        {
            _logger.LogDebug("SSH host key received: {FingerPrint} ({KeyLength} bit {HostKeyName})",
                BitConverter.ToString(e.FingerPrint).Replace("-", ":"),
                e.KeyLength, e.HostKeyName);
            e.CanTrust = true;
        };

        client.Connect();

        // Verify socat is available on the remote host
        var checkResult = client.RunCommand("which socat 2>/dev/null");
        if (checkResult.ExitStatus != 0 || string.IsNullOrWhiteSpace(checkResult.Result))
        {
            client.Disconnect();
            client.Dispose();
            throw new InvalidOperationException(
                $"socat is not installed on the remote host ({host}). " +
                "Install it with: apt-get install socat (Debian/Ubuntu) or yum install socat (RHEL/CentOS).");
        }

        // Start a local TCP listener — Docker.DotNet will connect here
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var localPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        var entry = new SshTunnelEntry(client, listener, localPort, remoteSocketPath, _logger);

        _logger.LogInformation(
            "SSH tunnel created: localhost:{LocalPort} → socat STDIO → {RemoteSocket} via {Host}:{SshPort}",
            localPort, remoteSocketPath, host, port);

        return entry;
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

        return new ConnectionInfo(host, port, username, auth)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    private static PrivateKeyAuthenticationMethod CreatePrivateKeyAuth(string username, string privateKeyContent)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKeyContent));
        var keyFile = new PrivateKeyFile(stream);
        return new PrivateKeyAuthenticationMethod(username, keyFile);
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

    /// <summary>
    /// Represents an active SSH tunnel with a local TCP listener.
    /// Each accepted TCP connection spawns an SSH exec channel running
    /// "socat STDIO UNIX-CONNECT:{socketPath}" for bidirectional streaming.
    /// </summary>
    private sealed class SshTunnelEntry : IDisposable
    {
        private readonly SshClient _client;
        private readonly TcpListener _listener;
        private readonly string _remoteSocketPath;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;

        public int LocalPort { get; }
        public Uri LocalUri => new($"tcp://localhost:{LocalPort}");
        public bool IsActive => _client.IsConnected && !_cts.IsCancellationRequested;

        public SshTunnelEntry(
            SshClient client,
            TcpListener listener,
            int localPort,
            string remoteSocketPath,
            ILogger logger)
        {
            _client = client;
            _listener = listener;
            _remoteSocketPath = remoteSocketPath;
            _logger = logger;
            LocalPort = localPort;

            // Start accepting connections in the background
            _acceptLoop = Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        }

        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                    // Handle each connection independently — don't await
                    _ = Task.Run(() => HandleConnectionAsync(tcpClient, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (ObjectDisposedException)
            {
                // Listener was stopped
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSH tunnel accept loop failed on localhost:{LocalPort}", LocalPort);
            }
        }

        private async Task HandleConnectionAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            using var _ = tcpClient;
            try
            {
                if (!_client.IsConnected)
                {
                    _logger.LogWarning("SSH client disconnected, cannot handle connection on localhost:{LocalPort}", LocalPort);
                    return;
                }

                using var command = _client.CreateCommand($"socat STDIO UNIX-CONNECT:{_remoteSocketPath}");
                var asyncResult = command.BeginExecute();

                using var sshInput = command.CreateInputStream();
                var sshOutput = command.OutputStream;
                var networkStream = tcpClient.GetStream();

                // Bidirectional pipe: TCP ↔ SSH exec channel
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                var toSsh = CopyStreamAsync(networkStream, sshInput, linkedCts.Token);
                var fromSsh = CopyStreamAsync(sshOutput, networkStream, linkedCts.Token);

                // When either direction finishes, cancel the other
                await Task.WhenAny(toSsh, fromSsh);
                await linkedCts.CancelAsync();

                // Wait for both to finish (they should complete quickly after cancellation)
                await Task.WhenAll(
                    toSsh.ContinueWith(_ => { }, TaskScheduler.Default),
                    fromSsh.ContinueWith(_ => { }, TaskScheduler.Default));

                command.EndExecute(asyncResult);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "SSH tunnel connection ended on localhost:{LocalPort}", LocalPort);
            }
        }

        private static async Task CopyStreamAsync(Stream source, Stream destination, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await source.ReadAsync(buffer, cancellationToken);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (bytesRead == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* ignore */ }
            try { _acceptLoop.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
            try { _client.Disconnect(); } catch { /* ignore */ }
            try { _client.Dispose(); } catch { /* ignore */ }
            _cts.Dispose();
        }
    }
}
