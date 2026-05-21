namespace ReadyStackGo.Application.Snmp;

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
    /// explicitly via configuration or the settings UI.
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
    /// IANA-assigned enterprise root 1.3.6.1.4.1.65846.1 (PEN 65846, ReadyStackGo).
    /// Enterprise Number once issued.
    /// </summary>
    public string RootOid { get; set; } = "1.3.6.1.4.1.65846.1";

    /// <summary>
    /// SNMPv2c community string. Requests that arrive with a different community
    /// are silently dropped (no response sent). Empty string disables v2c
    /// entirely — only SNMPv3 connections are accepted.
    /// </summary>
    public string Community { get; set; } = string.Empty;

    /// <summary>
    /// SNMPv3 USM users that may authenticate. An empty list disables v3 — only
    /// v2c with a matching community is accepted.
    /// </summary>
    public List<SnmpV3UserOption> V3Users { get; set; } = new();
}

/// <summary>
/// SNMPv3 USM user — name plus optional auth and priv credentials. The
/// passphrase is read from configuration at startup and key-localized by
/// SharpSnmpLib using the engine ID. Storing the passphrase as plain text in
/// configuration is acceptable for v0.64; rotating to credential encryption
/// (CredentialEncryptionService) is a follow-up.
/// </summary>
public class SnmpV3UserOption
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// "sha1" or "sha256". Empty / null = NoAuth (level noAuthNoPriv).
    /// </summary>
    public string AuthProtocol { get; set; } = string.Empty;
    public string AuthPassphrase { get; set; } = string.Empty;

    /// <summary>
    /// "aes128", "aes192", "aes256" or "des". Empty / null = NoPriv (only valid
    /// with NoAuth or AuthNoPriv level).
    /// </summary>
    public string PrivProtocol { get; set; } = string.Empty;
    public string PrivPassphrase { get; set; } = string.Empty;
}
