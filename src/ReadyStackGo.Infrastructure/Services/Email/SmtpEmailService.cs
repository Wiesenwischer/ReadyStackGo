using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using ReadyStackGo.Application.Services.Email;

namespace ReadyStackGo.Infrastructure.Services.Email;

/// <summary>
/// Sends email over SMTP using MailKit. Failures are returned as <see cref="EmailSendResult"/>
/// rather than thrown, so callers (e.g. domain-event handlers) can degrade gracefully.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly ISmtpSettingsService _settingsService;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(ISmtpSettingsService settingsService, ILogger<SmtpEmailService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);

        if (!settings.Enabled)
        {
            return new EmailSendResult(false, "Email sending is disabled.");
        }

        if (!settings.IsComplete())
        {
            return new EmailSendResult(false, "SMTP configuration is incomplete.");
        }

        return await SendInternalAsync(settings, toAddress, subject, htmlBody, cancellationToken);
    }

    public Task<EmailSendResult> SendTestAsync(
        SmtpSettings settings,
        string toAddress,
        CancellationToken cancellationToken = default)
    {
        return SendInternalAsync(
            settings,
            toAddress,
            "ReadyStackGo SMTP test",
            "<p>This is a test email from ReadyStackGo. Your SMTP configuration works.</p>",
            cancellationToken);
    }

    private async Task<EmailSendResult> SendInternalAsync(
        SmtpSettings settings,
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(toAddress));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();

            var socketOptions = settings.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            return new EmailSendResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToAddress}", toAddress);
            return new EmailSendResult(false, ex.Message);
        }
    }
}
