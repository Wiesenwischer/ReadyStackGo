namespace ReadyStackGo.Application.Services.Email;

/// <summary>
/// Reads and persists the SMTP configuration. Implementations store the password
/// encrypted at rest and return it decrypted in <see cref="GetAsync"/>.
/// </summary>
public interface ISmtpSettingsService
{
    /// <summary>Gets the current SMTP settings (password decrypted).</summary>
    Task<SmtpSettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the SMTP settings (password encrypted at rest).</summary>
    Task SaveAsync(SmtpSettings settings, CancellationToken cancellationToken = default);

    /// <summary>True if email is enabled and the configuration is complete enough to send.</summary>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);
}
