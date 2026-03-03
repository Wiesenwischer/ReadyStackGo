namespace ReadyStackGo.Application.UseCases.System.UpdateHealthNotificationSettings;

public record UpdateHealthNotificationSettingsResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}
