using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Snmp;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// Emits SNMP v2c traps via UDP to every receiver listed in
/// <see cref="SnmpSettings.TrapReceivers"/>. Resolves the live settings
/// through a DI scope each time so admin edits in the WebUI take effect
/// immediately, without re-instantiating this singleton.
/// </summary>
public sealed class SnmpTrapEmitter : ISnmpTrapEmitter
{
    private static readonly DateTime ProcessStartedAt = DateTime.UtcNow;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SnmpTrapEmitter> _logger;
    private int _requestIdCounter;

    public SnmpTrapEmitter(
        IServiceScopeFactory scopeFactory,
        ILogger<SnmpTrapEmitter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EmitAsync(SnmpTrap trap, CancellationToken cancellationToken = default)
    {
        SnmpSettings settings;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISnmpSettingsRepository>();
            settings = repo.GetOrCreate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SNMP settings while emitting trap");
            return;
        }

        if (!settings.Enabled)
        {
            _logger.LogDebug("Skipping trap emission — SNMP agent is disabled");
            return;
        }
        if (string.IsNullOrWhiteSpace(settings.Community))
        {
            _logger.LogDebug("Skipping trap emission — no v2c community configured");
            return;
        }

        var receivers = settings.ParseTrapReceivers().ToList();
        if (receivers.Count == 0)
        {
            _logger.LogDebug("Skipping trap emission — no receivers configured");
            return;
        }

        var trapOid = new ObjectIdentifier(CombineOid(settings.RootOid, trap.TrapOid));
        var variables = trap.Variables
            .Select(v => new Variable(
                new ObjectIdentifier(CombineOid(settings.RootOid, v.Oid)),
                v.Type switch
                {
                    SnmpTrapValueType.Integer32 => (ISnmpData)new Integer32(int.TryParse(v.Value, out var i) ? i : 0),
                    _ => new OctetString(v.Value ?? string.Empty),
                }))
            .ToList();

        var uptime = (uint)Math.Min(
            uint.MaxValue,
            (long)((DateTime.UtcNow - ProcessStartedAt).TotalMilliseconds * 10));

        var community = new OctetString(settings.Community);
        var requestId = Interlocked.Increment(ref _requestIdCounter);

        foreach (var receiver in receivers)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (!IPAddress.TryParse(receiver.Host, out var ip))
            {
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(receiver.Host, cancellationToken).ConfigureAwait(false);
                    ip = hostEntry.AddressList.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Trap receiver '{Host}' could not be resolved", receiver.Host);
                    continue;
                }
            }
            if (ip is null) continue;

            var endpoint = new IPEndPoint(ip, receiver.Port);

            try
            {
                await Messenger.SendTrapV2Async(
                    requestId: requestId,
                    version: VersionCode.V2,
                    receiver: endpoint,
                    community: community,
                    enterprise: trapOid,
                    timestamp: uptime,
                    variables: variables).ConfigureAwait(false);

                _logger.LogInformation(
                    "Sent SNMP v2c trap {TrapOid} to {Receiver}",
                    trap.TrapOid, endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SNMP trap to {Receiver}", endpoint);
            }
        }
    }

    private static string CombineOid(string root, string suffix)
    {
        var rootTrim = root.TrimEnd('.');
        var suffixTrim = suffix.TrimStart('.');
        return string.IsNullOrEmpty(suffixTrim) ? rootTrim : $"{rootTrim}.{suffixTrim}";
    }
}
