using MediatR;

namespace ReadyStackGo.Application.UseCases.System.UpdateHealthNotificationSettings;

public record UpdateHealthNotificationSettingsCommand : IRequest<UpdateHealthNotificationSettingsResponse>
{
    public int CooldownSeconds { get; init; }
}
