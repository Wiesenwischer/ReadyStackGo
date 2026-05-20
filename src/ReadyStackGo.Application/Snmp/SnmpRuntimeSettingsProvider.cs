using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Application.Snmp;

public sealed class SnmpRuntimeSettingsProvider : ISnmpRuntimeSettingsProvider
{
    private readonly ISnmpSettingsRepository _settings;
    private readonly ISnmpV3UserRepository _users;
    private readonly ICredentialEncryptionService _encryption;

    public SnmpRuntimeSettingsProvider(
        ISnmpSettingsRepository settings,
        ISnmpV3UserRepository users,
        ICredentialEncryptionService encryption)
    {
        _settings = settings;
        _users = users;
        _encryption = encryption;
    }

    public SnmpRuntimeSettings Load()
    {
        var s = _settings.GetOrCreate();
        var runtimeUsers = _users.GetAll()
            .Select(u => new SnmpRuntimeV3User(
                Name: u.Name,
                AuthProtocol: u.AuthProtocol == SnmpAuthProtocol.None ? string.Empty : u.AuthProtocol.ToString().ToLowerInvariant(),
                AuthPassphrase: u.AuthProtocol == SnmpAuthProtocol.None ? string.Empty : SafeDecrypt(u.AuthPassphraseEncrypted),
                PrivProtocol: u.PrivProtocol == SnmpPrivProtocol.None ? string.Empty : u.PrivProtocol.ToString().ToLowerInvariant(),
                PrivPassphrase: u.PrivProtocol == SnmpPrivProtocol.None ? string.Empty : SafeDecrypt(u.PrivPassphraseEncrypted)))
            .ToList();

        return new SnmpRuntimeSettings(
            Enabled: s.Enabled,
            Port: s.Port,
            ListenAddress: s.ListenAddress,
            RootOid: s.RootOid,
            Community: s.Community,
            V3Users: runtimeUsers);
    }

    private string SafeDecrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        try { return _encryption.Decrypt(ciphertext); }
        catch { return string.Empty; }
    }
}
