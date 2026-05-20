using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// UDP-based SNMP agent that listens for GET / GETNEXT / GETBULK requests and
/// answers them out of the configured <see cref="IOidTree"/>.
///
/// Feature 1 scope: end-to-end UDP path with proper SNMP message
/// encoding/decoding via SharpSnmpLib. The OID tree is empty so every request
/// receives noSuchObject / endOfMibView. v2c community strings and v3
/// auth/priv handling come in Features 3 and 4; this commit accepts any
/// community and v1/v2c messages only.
/// </summary>
public sealed class SnmpAgent : IAsyncDisposable
{
    private readonly SnmpAgentOptions _options;
    private readonly IOidTree _oidTree;
    private readonly ILogger<SnmpAgent> _logger;
    private readonly UserRegistry _userRegistry = new();

    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    public SnmpAgent(
        IOptions<SnmpAgentOptions> options,
        IOidTree oidTree,
        ILogger<SnmpAgent> logger)
    {
        _options = options.Value;
        _oidTree = oidTree;
        _logger = logger;
    }

    public bool IsRunning => _udpClient is not null;

    public IPEndPoint? BoundEndpoint =>
        _udpClient?.Client.LocalEndPoint as IPEndPoint;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SNMP agent disabled via configuration (Snmp:Enabled = false)");
            return Task.CompletedTask;
        }

        if (_udpClient is not null)
        {
            _logger.LogWarning("SNMP agent is already running on {Endpoint}", BoundEndpoint);
            return Task.CompletedTask;
        }

        if (!IPAddress.TryParse(_options.ListenAddress, out var address))
        {
            throw new InvalidOperationException(
                $"Snmp:ListenAddress '{_options.ListenAddress}' is not a valid IP address.");
        }

        var endpoint = new IPEndPoint(address, _options.Port);
        _udpClient = new UdpClient(endpoint);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

        _logger.LogInformation(
            "SNMP agent listening on UDP {Address}:{Port} (root OID {RootOid})",
            _options.ListenAddress, _options.Port, _options.RootOid);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
        {
            return;
        }

        try
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _udpClient?.Close();
            if (_receiveTask is not null)
            {
                try
                {
                    await _receiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* expected */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping SNMP agent");
        }
        finally
        {
            _udpClient?.Dispose();
            _cts.Dispose();
            _udpClient = null;
            _cts = null;
            _receiveTask = null;
            _logger.LogInformation("SNMP agent stopped");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _udpClient is not null)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SNMP agent receive loop error");
                continue;
            }

            await ProcessDatagramAsync(result.Buffer, result.RemoteEndPoint, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task ProcessDatagramAsync(byte[] buffer, IPEndPoint remote, CancellationToken ct)
    {
        IList<ISnmpMessage> messages;
        try
        {
            messages = MessageFactory.ParseMessages(buffer, _userRegistry);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Discarded malformed SNMP datagram from {Remote}", remote);
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                var response = BuildResponse(message);
                if (response is null)
                {
                    continue;
                }

                var responseBytes = response.ToBytes();
                if (_udpClient is null)
                {
                    return;
                }

                await _udpClient
                    .SendAsync(responseBytes, responseBytes.Length, remote)
                    .WaitAsync(ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to answer SNMP request from {Remote}", remote);
            }
        }
    }

    private ISnmpMessage? BuildResponse(ISnmpMessage request)
    {
        // v3 (auth/priv) ships in Feature 4. For Feature 1 we handle v1/v2c only
        // and ignore anything else.
        if (request.Version != VersionCode.V1 && request.Version != VersionCode.V2)
        {
            return null;
        }

        var pdu = request.Pdu();
        var responseVariables = new List<Variable>(pdu.Variables.Count);

        foreach (var variable in pdu.Variables)
        {
            switch (pdu.TypeCode)
            {
                case SnmpType.GetRequestPdu:
                    var value = _oidTree.Get(variable.Id);
                    responseVariables.Add(new Variable(
                        variable.Id,
                        value ?? (ISnmpData)new NoSuchObject()));
                    break;

                case SnmpType.GetNextRequestPdu:
                case SnmpType.GetBulkRequestPdu:
                    var next = _oidTree.GetNext(variable.Id);
                    if (next.HasValue)
                    {
                        responseVariables.Add(new Variable(next.Value.Oid, next.Value.Value));
                    }
                    else
                    {
                        responseVariables.Add(new Variable(variable.Id, new EndOfMibView()));
                    }
                    break;

                default:
                    // SET / TRAP / INFORM are not supported in this milestone.
                    return null;
            }
        }

        return new ResponseMessage(
            request.RequestId(),
            request.Version,
            request.Community(),
            ErrorCode.NoError,
            0,
            responseVariables);
    }
}
