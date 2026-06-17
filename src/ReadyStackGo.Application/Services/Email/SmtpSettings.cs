namespace ReadyStackGo.Application.Services.Email;

/// <summary>
/// SMTP configuration as used at runtime. The <see cref="Password"/> is the plaintext
/// password; it is only ever held in memory and is stored encrypted at rest.
/// </summary>
public class SmtpSettings
{
    /// <summary>Whether outbound email is enabled. When false, no mail is sent.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    /// <summary>Use explicit STARTTLS upgrade. When false, the client auto-negotiates TLS.</summary>
    public bool UseStartTls { get; set; } = true;

    public string? Username { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "ReadyStackGo";

    /// <summary>True if the minimum fields required to send mail are present.</summary>
    public bool IsComplete() =>
        !string.IsNullOrWhiteSpace(Host)
        && Port > 0
        && !string.IsNullOrWhiteSpace(FromAddress);
}
