using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// PreProcessor that enforces wizard timeout on all wizard endpoints.
/// Blocks requests when the 5-minute setup window has expired.
/// </summary>
public class WizardTimeoutPreProcessor<TRequest> : IPreProcessor<TRequest>
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct)
    {
        // Skip timeout check for:
        // - GET /api/wizard/status (needs to return timeout info)
        // - POST /api/wizard/start-window (explicitly starts new window)
        var path = ctx.HttpContext.Request.Path.Value ?? "";
        if (path.EndsWith("/status", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/start-window", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var timeoutService = ctx.HttpContext.RequestServices.GetRequiredService<IWizardTimeoutService>();
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<WizardTimeoutPreProcessor<TRequest>>>();

        if (await timeoutService.IsTimedOutAsync())
        {
            // Log the timeout access attempt
            var clientIp = ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            logger.LogWarning(
                "Wizard timeout: Blocked request to {Path} from {ClientIp}. Setup window expired.",
                path,
                clientIp);

            // Reset the wizard state (clears partial configuration)
            await timeoutService.ResetTimeoutAsync();

            // Send error response
            await ctx.HttpContext.Response.SendAsync(
                new WizardTimeoutErrorResponse
                {
                    StatusCode = 403,
                    Message = "Wizard timeout expired. Please start a new setup window.",
                    IsTimedOut = true
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
}
