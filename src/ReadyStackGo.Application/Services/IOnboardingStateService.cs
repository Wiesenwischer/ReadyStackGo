namespace ReadyStackGo.Application.Services;

public interface IOnboardingStateService
{
    Task<bool> IsDismissedAsync(CancellationToken cancellationToken = default);
    Task DismissAsync(CancellationToken cancellationToken = default);
}
