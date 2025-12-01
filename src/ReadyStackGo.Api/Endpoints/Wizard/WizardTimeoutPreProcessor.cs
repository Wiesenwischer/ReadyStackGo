using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// PreProcessor that enforces wizard timeout on all wizard endpoints.
/// Blocks requests when the 5-minute setup window has expired or wizard is locked.
/// Lock can only be reset by restarting the container.
/// </summary>
public class WizardTimeoutPreProcessor<TRequest> : IPreProcessor<TRequest>
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct)
    {
        // Skip timeout check for GET /api/wizard/status (needs to return timeout/lock info)
        var path = ctx.HttpContext.Request.Path.Value ?? "";
        if (path.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var timeoutService = ctx.HttpContext.RequestServices.GetRequiredService<IWizardTimeoutService>();
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<WizardTimeoutPreProcessor<TRequest>>>();

        var timeoutInfo = await timeoutService.GetTimeoutInfoAsync();

        if (timeoutInfo.IsTimedOut || timeoutInfo.IsLocked)
        {
            // Log the blocked access attempt
            var clientIp = ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            logger.LogWarning(
                "Wizard locked: Blocked request to {Path} from {ClientIp}. Setup window expired. Restart container to reset.",
                path,
                clientIp);

            // Send error response - user must restart container
            await ctx.HttpContext.Response.SendAsync(
                new WizardTimeoutErrorResponse
                {
                    StatusCode = 403,
                    Message = "Setup window expired. Restart the container to try again.",
                    IsTimedOut = true,
                    IsLocked = timeoutInfo.IsLocked
                },
                403,
                cancellation: ct);
        }
    }
}

/// <summary>
/// Error response returned when wizard timeout has expired.
/// </summary>
public class WizardTimeoutErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsTimedOut { get; set; }
    public bool IsLocked { get; set; }
}
