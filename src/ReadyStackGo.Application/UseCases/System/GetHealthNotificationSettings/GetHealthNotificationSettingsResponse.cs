namespace ReadyStackGo.Application.UseCases.System.GetHealthNotificationSettings;

public record GetHealthNotificationSettingsResponse
{
    public int CooldownSeconds { get; init; }
}
