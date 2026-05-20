using ReadyStackGo.Domain.SharedKernel;

namespace ReadyStackGo.Domain.Snmp;

/// <summary>
/// Singleton-aggregate that holds the live SNMP-agent configuration.
/// Only one row exists in the database (Id = 1). Editing it via the API
/// triggers <see cref="SnmpSettingsChanged"/> so the agent can re-bind its
/// listener without a container restart.
/// </summary>
public class SnmpSettings : AggregateRoot<int>
{
    public const int SingletonId = 1;

    public bool Enabled { get; private set; }
    public int Port { get; private set; }
    public string ListenAddress { get; private set; } = "0.0.0.0";
    public string RootOid { get; private set; } = "1.3.6.1.4.1.99999.1";
    public string Community { get; private set; } = string.Empty;
    public string TrapReceivers { get; private set; } = string.Empty;

    // EF Core
    protected SnmpSettings() { }

    public static SnmpSettings CreateDefault()
    {
        return new SnmpSettings
        {
            Id = SingletonId,
            Enabled = false,
            Port = 1161,
            ListenAddress = "0.0.0.0",
            RootOid = "1.3.6.1.4.1.99999.1",
            Community = string.Empty,
            TrapReceivers = string.Empty,
        };
    }

    public bool Update(
        bool enabled,
        int port,
        string listenAddress,
        string rootOid,
        string community,
        string trapReceivers)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(listenAddress))
            throw new ArgumentException("Listen address is required.", nameof(listenAddress));
        if (string.IsNullOrWhiteSpace(rootOid))
            throw new ArgumentException("Root OID is required.", nameof(rootOid));

        var changed = Enabled != enabled
                   || Port != port
                   || ListenAddress != listenAddress
                   || RootOid != rootOid
                   || Community != (community ?? string.Empty)
                   || TrapReceivers != (trapReceivers ?? string.Empty);

        if (!changed) return false;

        Enabled = enabled;
        Port = port;
        ListenAddress = listenAddress;
        RootOid = rootOid;
        Community = community ?? string.Empty;
        TrapReceivers = trapReceivers ?? string.Empty;
        IncrementVersion();
        AddDomainEvent(new SnmpSettingsChanged());
        return true;
    }

    /// <summary>
    /// Trap receivers parsed from <see cref="TrapReceivers"/> (comma- or
    /// newline-separated "host[:port]" entries). Empty list when disabled.
    /// </summary>
    public IEnumerable<TrapReceiver> ParseTrapReceivers()
    {
        if (string.IsNullOrWhiteSpace(TrapReceivers)) yield break;

        var entries = TrapReceivers.Split(
            new[] { ',', '\n', '\r', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var entry in entries)
        {
            var parts = entry.Split(':', 2, StringSplitOptions.TrimEntries);
            var host = parts[0];
            if (string.IsNullOrWhiteSpace(host)) continue;
            var port = parts.Length == 2 && int.TryParse(parts[1], out var p) ? p : 162;
            yield return new TrapReceiver(host, port);
        }
    }
}

public record TrapReceiver(string Host, int Port);
