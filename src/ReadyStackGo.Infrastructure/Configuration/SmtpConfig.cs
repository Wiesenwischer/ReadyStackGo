namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// SMTP configuration stored in rsgo.smtp.json. The password is stored encrypted
/// (see <see cref="EncryptedPassword"/>) and never in plaintext.
/// </summary>
public class SmtpConfig
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }

    /// <summary>AES-encrypted SMTP password (via ICredentialEncryptionService). Null if none.</summary>
    public string? EncryptedPassword { get; set; }

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "ReadyStackGo";
}
