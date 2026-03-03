using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.System.UpdateHealthNotificationSettings;

public class UpdateHealthNotificationSettingsHandler
    : IRequestHandler<UpdateHealthNotificationSettingsCommand, UpdateHealthNotificationSettingsResponse>
{
    private readonly ISystemConfigService _systemConfigService;

    public UpdateHealthNotificationSettingsHandler(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    public async Task<UpdateHealthNotificationSettingsResponse> Handle(
        UpdateHealthNotificationSettingsCommand request, CancellationToken cancellationToken)
    {
        if (request.CooldownSeconds < 60 || request.CooldownSeconds > 3600)
        {
            return new UpdateHealthNotificationSettingsResponse
            {
                Success = false,
                Message = "Cooldown must be between 60 and 3600 seconds."
            };
        }

        await _systemConfigService.SetHealthNotificationCooldownSecondsAsync(request.CooldownSeconds);

        return new UpdateHealthNotificationSettingsResponse
        {
            Success = true,
            Message = "Health notification settings updated."
        };
    }
}
