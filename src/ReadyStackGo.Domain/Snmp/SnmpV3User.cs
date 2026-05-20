using ReadyStackGo.Domain.SharedKernel;

namespace ReadyStackGo.Domain.Snmp;

/// <summary>
/// USM (User-based Security Model) user for SNMPv3 authentication.
///
/// Passphrases are stored encrypted with the same CredentialEncryptionService
/// that protects Docker registry credentials and SSH keys, so they never sit
/// in the database in plain text.
/// </summary>
public class SnmpV3User : AggregateRoot<Guid>
{
    public string Name { get; private set; } = null!;
    public SnmpAuthProtocol AuthProtocol { get; private set; }
    public string AuthPassphraseEncrypted { get; private set; } = string.Empty;
    public SnmpPrivProtocol PrivProtocol { get; private set; }
    public string PrivPassphraseEncrypted { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // EF Core
    protected SnmpV3User() { }

    public static SnmpV3User Create(
        string name,
        SnmpAuthProtocol authProtocol,
        string authPassphraseEncrypted,
        SnmpPrivProtocol privProtocol,
        string privPassphraseEncrypted)
    {
        Validate(name, authProtocol, authPassphraseEncrypted, privProtocol, privPassphraseEncrypted);

        return new SnmpV3User
        {
            Id = Guid.NewGuid(),
            Name = name,
            AuthProtocol = authProtocol,
            AuthPassphraseEncrypted = authPassphraseEncrypted,
            PrivProtocol = privProtocol,
            PrivPassphraseEncrypted = privPassphraseEncrypted,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Update(
        SnmpAuthProtocol authProtocol,
        string? authPassphraseEncryptedOrNullToKeep,
        SnmpPrivProtocol privProtocol,
        string? privPassphraseEncryptedOrNullToKeep)
    {
        var auth = authPassphraseEncryptedOrNullToKeep ?? AuthPassphraseEncrypted;
        var priv = privPassphraseEncryptedOrNullToKeep ?? PrivPassphraseEncrypted;
        Validate(Name, authProtocol, auth, privProtocol, priv);

        AuthProtocol = authProtocol;
        AuthPassphraseEncrypted = auth;
        PrivProtocol = privProtocol;
        PrivPassphraseEncrypted = priv;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private static void Validate(
        string name,
        SnmpAuthProtocol authProtocol,
        string authPassphraseEncrypted,
        SnmpPrivProtocol privProtocol,
        string privPassphraseEncrypted)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("User name is required.", nameof(name));
        if (authProtocol != SnmpAuthProtocol.None && string.IsNullOrWhiteSpace(authPassphraseEncrypted))
            throw new ArgumentException("Auth passphrase is required when an auth protocol is set.");
        if (privProtocol != SnmpPrivProtocol.None && authProtocol == SnmpAuthProtocol.None)
            throw new ArgumentException("Privacy requires authentication — set an auth protocol when using priv.");
        if (privProtocol != SnmpPrivProtocol.None && string.IsNullOrWhiteSpace(privPassphraseEncrypted))
            throw new ArgumentException("Priv passphrase is required when a priv protocol is set.");
    }
}

public enum SnmpAuthProtocol
{
    None = 0,
    Md5 = 1,
    Sha1 = 2,
    Sha256 = 3,
    Sha384 = 4,
    Sha512 = 5,
}

public enum SnmpPrivProtocol
{
    None = 0,
    Des = 1,
    Aes128 = 2,
    Aes192 = 3,
    Aes256 = 4,
}
