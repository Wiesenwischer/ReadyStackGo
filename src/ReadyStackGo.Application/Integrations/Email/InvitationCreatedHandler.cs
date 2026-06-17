using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Email;
using ReadyStackGo.Domain.IdentityAccess.Invitations;

namespace ReadyStackGo.Application.Integrations.Email;

/// <summary>
/// Sends the invitation email when an <see cref="InvitationCreated"/> domain event fires.
/// Best-effort: an email failure is logged but does not break the invitation flow (the
/// admin can revoke and re-invite).
/// </summary>
public sealed class InvitationCreatedHandler
    : INotificationHandler<DomainEventNotification<InvitationCreated>>
{
    private readonly IEmailService _emailService;
    private readonly ISystemConfigService _systemConfig;
    private readonly ILogger<InvitationCreatedHandler> _logger;

    public InvitationCreatedHandler(
        IEmailService emailService,
        ISystemConfigService systemConfig,
        ILogger<InvitationCreatedHandler> logger)
    {
        _emailService = emailService;
        _systemConfig = systemConfig;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<InvitationCreated> notification, CancellationToken ct)
    {
        var evt = notification.DomainEvent;

        var baseUrl = (await _systemConfig.GetBaseUrlAsync()).TrimEnd('/');
        var link = $"{baseUrl}/accept-invite?token={Uri.EscapeDataString(evt.PlainToken)}";

        var result = await _emailService.SendAsync(
            evt.Email.Value,
            "You have been invited to ReadyStackGo",
            $"<p>You have been invited to ReadyStackGo. Click the link below to set your password and activate your account:</p>" +
            $"<p><a href=\"{link}\">Accept invitation</a></p>" +
            $"<p>If you did not expect this invitation, you can ignore this email.</p>",
            ct);

        if (!result.Success)
        {
            _logger.LogError(
                "Failed to send invitation email to {Email}: {Error}",
                evt.Email.Value, result.Error);
        }
    }
}
