using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.System.GetHealthNotificationSettings;

public class GetHealthNotificationSettingsHandler
    : IRequestHandler<GetHealthNotificationSettingsQuery, GetHealthNotificationSettingsResponse>
{
    private readonly ISystemConfigService _systemConfigService;

    public GetHealthNotificationSettingsHandler(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    public async Task<GetHealthNotificationSettingsResponse> Handle(
        GetHealthNotificationSettingsQuery request, CancellationToken cancellationToken)
    {
        var cooldownSeconds = await _systemConfigService.GetHealthNotificationCooldownSecondsAsync();

        return new GetHealthNotificationSettingsResponse
        {
            CooldownSeconds = cooldownSeconds
        };
    }
}
