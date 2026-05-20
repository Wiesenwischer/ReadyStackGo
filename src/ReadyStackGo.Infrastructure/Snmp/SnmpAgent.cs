using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// UDP-based SNMP agent that listens for GET / GETNEXT / GETBULK requests and
/// answers them out of the configured <see cref="IOidTree"/>.
///
/// v1/v2c: community string match required (configurable, dropped on
/// mismatch). v3: USM user registry built from configuration, with SHA / AES
/// auth and priv providers per RFC 3414 / 3826.
/// </summary>
public sealed class SnmpAgent : IAsyncDisposable
{
    private readonly SnmpAgentOptions _options;
    private readonly IOidTree _oidTree;
    private readonly ILogger<SnmpAgent> _logger;
    private UserRegistry _userRegistry = new();

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

        _userRegistry = BuildUserRegistry(_options, _logger);

        var endpoint = new IPEndPoint(address, _options.Port);
        _udpClient = new UdpClient(endpoint);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

        _logger.LogInformation(
            "SNMP agent listening on UDP {Address}:{Port} (root OID {RootOid}, v2c {V2cState}, v3 users {V3Count})",
            _options.ListenAddress, _options.Port, _options.RootOid,
            string.IsNullOrWhiteSpace(_options.Community) ? "disabled" : "enabled",
            _options.V3Users?.Count ?? 0);
        return Task.CompletedTask;
    }

    private static UserRegistry BuildUserRegistry(SnmpAgentOptions options, ILogger logger)
    {
        var registry = new UserRegistry();
        if (options.V3Users is null) return registry;

        foreach (var user in options.V3Users)
        {
            if (string.IsNullOrWhiteSpace(user.Name)) continue;

            var name = new OctetString(user.Name);
            IAuthenticationProvider auth = string.IsNullOrWhiteSpace(user.AuthProtocol)
                ? DefaultAuthenticationProvider.Instance
                : CreateAuthProvider(user.AuthProtocol, user.AuthPassphrase, logger, user.Name);

            IPrivacyProvider priv = string.IsNullOrWhiteSpace(user.PrivProtocol)
                ? new DefaultPrivacyProvider(auth)
                : CreatePrivProvider(user.PrivProtocol, user.PrivPassphrase, auth, logger, user.Name);

            registry.Add(name, priv);
            logger.LogInformation("Registered SNMPv3 user '{User}' (auth {AuthProto}, priv {PrivProto})",
                user.Name,
                string.IsNullOrWhiteSpace(user.AuthProtocol) ? "none" : user.AuthProtocol,
                string.IsNullOrWhiteSpace(user.PrivProtocol) ? "none" : user.PrivProtocol);
        }
        return registry;
    }

    private static IAuthenticationProvider CreateAuthProvider(string protocol, string passphrase, ILogger logger, string userName)
    {
        var pass = new OctetString(passphrase ?? string.Empty);
        return protocol.ToLowerInvariant() switch
        {
            "sha" or "sha1" => new SHA1AuthenticationProvider(pass),
            "sha256" => new SHA256AuthenticationProvider(pass),
            "sha384" => new SHA384AuthenticationProvider(pass),
            "sha512" => new SHA512AuthenticationProvider(pass),
            "md5" => new MD5AuthenticationProvider(pass),
            _ => Fallback(logger, userName, protocol),
        };

        static IAuthenticationProvider Fallback(ILogger l, string u, string p)
        {
            l.LogWarning("Unknown SNMPv3 auth protocol '{Proto}' for user '{User}' — disabling authentication", p, u);
            return DefaultAuthenticationProvider.Instance;
        }
    }

    private static IPrivacyProvider CreatePrivProvider(string protocol, string passphrase, IAuthenticationProvider auth, ILogger logger, string userName)
    {
        var pass = new OctetString(passphrase ?? string.Empty);
        return protocol.ToLowerInvariant() switch
        {
            "aes" or "aes128" => new AESPrivacyProvider(pass, auth),
            "aes192" => new AES192PrivacyProvider(pass, auth),
            "aes256" => new AES256PrivacyProvider(pass, auth),
            "des" => new DESPrivacyProvider(pass, auth),
            _ => FallbackPriv(logger, userName, protocol, auth),
        };

        static IPrivacyProvider FallbackPriv(ILogger l, string u, string p, IAuthenticationProvider a)
        {
            l.LogWarning("Unknown SNMPv3 priv protocol '{Proto}' for user '{User}' — disabling privacy", p, u);
            return new DefaultPrivacyProvider(a);
        }
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
        // v3 messages are decoded + authenticated by MessageFactory using the
        // configured UserRegistry; if the user is unknown or the auth/priv keys
        // don't match, MessageFactory throws and the datagram is dropped
        // upstream. v2c / v1 are gated here by the community-string check.
        if (request.Version == VersionCode.V1 || request.Version == VersionCode.V2)
        {
            if (string.IsNullOrWhiteSpace(_options.Community))
            {
                // v2c disabled — only v3 accepted.
                return null;
            }
            if (request.Community().ToString() != _options.Community)
            {
                _logger.LogDebug("Dropped v{Version} request with mismatched community", (int)request.Version);
                return null;
            }
        }
        else if (request.Version != VersionCode.V3)
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
