namespace ReadyStackGo.Infrastructure.Snmp;

/// <summary>
/// Configuration for the SNMP agent listener.
///
/// Defaults to UDP/1161 (non-privileged) so the container can run as a non-root
/// user. Operators map host:161 → container:1161 in docker-compose.yml when they
/// want monitoring tools to use the classic SNMP port without configuring a
/// non-standard port.
/// </summary>
public class SnmpAgentOptions
{
    public const string SectionName = "Snmp";

    /// <summary>
    /// Whether the SNMP agent is enabled. Default: false — operators must opt in
    /// explicitly via configuration or the settings UI (Feature 3+).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// UDP port the agent listens on inside the container. Default 1161 to stay
    /// out of the privileged port range. The classic SNMP port 161 is reachable
    /// from outside via docker-compose port mapping.
    /// </summary>
    public int Port { get; set; } = 1161;

    /// <summary>
    /// IP address the listener binds to. Default "0.0.0.0" listens on all
    /// interfaces inside the container.
    /// </summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Root OID under which all RSGO managed objects live. Default is the
    /// placeholder 1.3.6.1.4.1.99999.1; replaced with the assigned IANA Private
    /// Enterprise Number once issued.
    /// </summary>
    public string RootOid { get; set; } = "1.3.6.1.4.1.99999.1";
}
