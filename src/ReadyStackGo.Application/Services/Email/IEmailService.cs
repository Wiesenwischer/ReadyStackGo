namespace ReadyStackGo.Application.Services.Email;

/// <summary>Result of an email send attempt.</summary>
public record EmailSendResult(bool Success, string? Error = null);

/// <summary>
/// Sends transactional emails (invitations, verification links, ...).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email using the currently saved and enabled SMTP settings.
    /// Returns a failure result (rather than throwing) when email is disabled or sending fails.
    /// </summary>
    Task<EmailSendResult> SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a test email using explicitly provided settings, to validate a configuration
    /// before it is saved (used by the wizard and the admin settings page).
    /// </summary>
    Task<EmailSendResult> SendTestAsync(
        SmtpSettings settings,
        string toAddress,
        CancellationToken cancellationToken = default);
}
