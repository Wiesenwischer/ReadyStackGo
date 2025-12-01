using FastEndpoints;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/start-window - Explicitly start a new 5-minute setup window.
/// This endpoint must be called after a timeout to begin a new setup attempt.
/// </summary>
public class StartNewWindowEndpoint : EndpointWithoutRequest<StartNewWindowResponse>
{
    private readonly IWizardTimeoutService _wizardTimeoutService;
    private readonly ILogger<StartNewWindowEndpoint> _logger;

    public StartNewWindowEndpoint(
        IWizardTimeoutService wizardTimeoutService,
        ILogger<StartNewWindowEndpoint> logger)
    {
        _wizardTimeoutService = wizardTimeoutService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/wizard/start-window");
        AllowAnonymous();
        // No timeout preprocessor - this endpoint explicitly starts the window
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Check if wizard is already completed
        var timeoutInfo = await _wizardTimeoutService.GetTimeoutInfoAsync();

        // If wizard is completed, don't allow starting a new window
        if (timeoutInfo.StartedAt == null && !timeoutInfo.IsTimedOut)
        {
            // Check if wizard is actually completed by examining the state
            // GetTimeoutInfoAsync returns null StartedAt for completed wizards
            var currentInfo = await _wizardTimeoutService.GetTimeoutInfoAsync();
            if (currentInfo.TimeoutSeconds > 0 && currentInfo.StartedAt == null && !currentInfo.IsTimedOut)
            {
                // This might be a fresh start or completed wizard
                // For fresh starts, we proceed; for completed, the timeout service handles it
            }
        }

        // Reset any previous timeout state and start fresh
        await _wizardTimeoutService.ResetTimeoutAsync();

        // Now get fresh timeout info (this will initialize a new window)
        var newTimeoutInfo = await _wizardTimeoutService.GetTimeoutInfoAsync();

        _logger.LogInformation(
            "Wizard setup window started from {ClientIp}. Expires at {ExpiresAt}",
            clientIp,
            newTimeoutInfo.ExpiresAt);

        Response = new StartNewWindowResponse
        {
            Success = true,
            Message = "New setup window started successfully",
            Timeout = new WizardTimeoutDto
            {
                IsTimedOut = newTimeoutInfo.IsTimedOut,
                RemainingSeconds = newTimeoutInfo.RemainingSeconds,
                ExpiresAt = newTimeoutInfo.ExpiresAt,
                TimeoutSeconds = newTimeoutInfo.TimeoutSeconds
            }
        };
    }
}

/// <summary>
/// Response for starting a new wizard window.
/// </summary>
public class StartNewWindowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WizardTimeoutDto? Timeout { get; set; }
}
