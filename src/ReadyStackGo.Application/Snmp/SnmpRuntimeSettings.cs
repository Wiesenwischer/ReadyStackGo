namespace ReadyStackGo.Application.Snmp;

/// <summary>
/// Snapshot of the SNMP-agent configuration the listener uses for one
/// lifecycle. Built from the DB (SnmpSettings + SnmpV3User repositories)
/// + decrypted passphrases by an Application-layer service so the Infrastructure
/// listener does not need direct repository or encryption-service access.
/// </summary>
public record SnmpRuntimeSettings(
    bool Enabled,
    int Port,
    string ListenAddress,
    string RootOid,
    string Community,
    IReadOnlyList<SnmpRuntimeV3User> V3Users,
    string EngineIdHex);

public record SnmpRuntimeV3User(
    string Name,
    string AuthProtocol,
    string AuthPassphrase,
    string PrivProtocol,
    string PrivPassphrase);

public interface ISnmpRuntimeSettingsProvider
{
    SnmpRuntimeSettings Load();
}
