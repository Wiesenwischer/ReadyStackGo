using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// UDP-based SNMP agent. Reads its settings + v3 users from the database via
/// <see cref="ISnmpRuntimeSettingsProvider"/> (resolved through a DI scope) so
/// admins can edit configuration in the WebUI without restarting the container.
/// </summary>
public sealed class SnmpAgent : IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOidTree _oidTree;
    private readonly ILogger<SnmpAgent> _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private SnmpRuntimeSettings? _currentSettings;
    private UserRegistry _userRegistry = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private CancellationToken _hostCancellationToken = CancellationToken.None;
    private int _engineBoots;
    private DateTime _startedAt = DateTime.UtcNow;

    public SnmpAgent(
        IServiceScopeFactory scopeFactory,
        IOidTree oidTree,
        ILogger<SnmpAgent> logger)
    {
        _scopeFactory = scopeFactory;
        _oidTree = oidTree;
        _logger = logger;
    }

    public bool IsRunning => _udpClient is not null;

    public IPEndPoint? BoundEndpoint => _udpClient?.Client.LocalEndPoint as IPEndPoint;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _hostCancellationToken = cancellationToken;
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = LoadSettings();
            await StartListenerAsync(settings).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Re-reads settings from the database and rebinds the listener if anything
    /// material changed. Used by the SnmpSettingsChanged notification handler.
    /// </summary>
    public async Task ReloadAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var settings = LoadSettings();
            await StopListenerInternalAsync().ConfigureAwait(false);
            await StartListenerAsync(settings).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopListenerInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _lifecycleLock.Dispose();
    }

    private SnmpRuntimeSettings LoadSettings()
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ISnmpRuntimeSettingsProvider>();
        return provider.Load();
    }

    private async Task StartListenerAsync(SnmpRuntimeSettings settings)
    {
        _currentSettings = settings;
        _userRegistry = BuildUserRegistry(settings, _logger);
        _engineBoots++;
        _startedAt = DateTime.UtcNow;

        if (!settings.Enabled)
        {
            _logger.LogInformation("SNMP agent disabled via settings");
            return;
        }

        if (!IPAddress.TryParse(settings.ListenAddress, out var address))
        {
            throw new InvalidOperationException(
                $"Snmp settings ListenAddress '{settings.ListenAddress}' is not a valid IP address.");
        }

        var endpoint = new IPEndPoint(address, settings.Port);
        _udpClient = new UdpClient(endpoint);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(_hostCancellationToken);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

        _logger.LogInformation(
            "SNMP agent listening on UDP {Address}:{Port} (root OID {RootOid}, v2c {V2cState}, v3 users {V3Count})",
            settings.ListenAddress, settings.Port, settings.RootOid,
            string.IsNullOrWhiteSpace(settings.Community) ? "disabled" : "enabled",
            settings.V3Users.Count);
    }

    private async Task StopListenerInternalAsync()
    {
        if (_cts is null) return;

        try
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _udpClient?.Close();
            if (_receiveTask is not null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
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

    private static UserRegistry BuildUserRegistry(SnmpRuntimeSettings settings, ILogger logger)
    {
        var registry = new UserRegistry();
        foreach (var user in settings.V3Users)
        {
            if (string.IsNullOrWhiteSpace(user.Name)) continue;

            IAuthenticationProvider auth = string.IsNullOrWhiteSpace(user.AuthProtocol)
                ? DefaultAuthenticationProvider.Instance
                : CreateAuthProvider(user.AuthProtocol, user.AuthPassphrase, logger, user.Name);

            IPrivacyProvider priv = string.IsNullOrWhiteSpace(user.PrivProtocol)
                ? new DefaultPrivacyProvider(auth)
                : CreatePrivProvider(user.PrivProtocol, user.PrivPassphrase, auth, logger, user.Name);

            registry.Add(new OctetString(user.Name), priv);
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

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _udpClient is not null)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }
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
                if (response is null) continue;

                var responseBytes = response.ToBytes();
                if (_udpClient is null) return;
                await _udpClient.SendAsync(responseBytes, responseBytes.Length, remote).WaitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to answer SNMP request from {Remote}", remote);
            }
        }
    }

    private ISnmpMessage? BuildResponse(ISnmpMessage request)
    {
        var settings = _currentSettings;
        if (settings is null) return null;

        if (request.Version == VersionCode.V1 || request.Version == VersionCode.V2)
        {
            if (string.IsNullOrWhiteSpace(settings.Community)) return null;
            if (request.Community().ToString() != settings.Community)
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
                    return null;
            }
        }

        if (request.Version == VersionCode.V3)
        {
            return BuildV3Response(request, responseVariables);
        }

        return new ResponseMessage(
            request.RequestId(),
            request.Version,
            request.Community(),
            ErrorCode.NoError,
            0,
            responseVariables);
    }

    private ResponseMessage? BuildV3Response(ISnmpMessage request, IList<Variable> responseVariables)
    {
        var v3 = ExtractV3Components(request);
        if (v3 is null)
        {
            _logger.LogDebug("Could not extract v3 components from message {TypeCode}", request.TypeCode());
            return null;
        }

        var (header, parameters, scope, privacy) = v3.Value;

        var ourEngineIdBytes = ParseHexBytes(_currentSettings!.EngineIdHex);
        if (ourEngineIdBytes.Length == 0)
        {
            _logger.LogWarning("v3 request received but settings have no EngineIdHex configured — dropping");
            return null;
        }
        var ourEngineId = new OctetString(ourEngineIdBytes);
        var engineTime = (int)Math.Min(int.MaxValue, (DateTime.UtcNow - _startedAt).TotalSeconds);

        var responsePdu = new ResponsePdu(
            request.RequestId(),
            ErrorCode.NoError,
            0,
            responseVariables);

        var contextEngineId = scope.ContextEngineId.GetRaw().Length == 0
            ? ourEngineId
            : scope.ContextEngineId;
        var responseScope = new Scope(contextEngineId, scope.ContextName, responsePdu);

        var responseParameters = new SecurityParameters(
            ourEngineId,
            new Integer32(_engineBoots),
            new Integer32(engineTime),
            parameters.UserName,
            new OctetString(string.Empty),
            new OctetString(string.Empty));

        var needAuthentication =
            (header.SecurityLevel & Levels.Authentication) == Levels.Authentication;

        try
        {
            return new ResponseMessage(
                VersionCode.V3,
                header,
                responseParameters,
                responseScope,
                privacy,
                needAuthentication,
                Array.Empty<byte>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to assemble SNMPv3 response — dropping request");
            return null;
        }
    }

    private static (Header Header, SecurityParameters Parameters, Scope Scope, IPrivacyProvider Privacy)?
        ExtractV3Components(ISnmpMessage message) => message switch
    {
        GetRequestMessage r     => (r.Header, r.Parameters, r.Scope, r.Privacy),
        GetNextRequestMessage r => (r.Header, r.Parameters, r.Scope, r.Privacy),
        GetBulkRequestMessage r => (r.Header, r.Parameters, r.Scope, r.Privacy),
        _ => null,
    };

    private static byte[] ParseHexBytes(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Array.Empty<byte>();
        try { return Convert.FromHexString(hex); }
        catch { return Array.Empty<byte>(); }
    }
}
