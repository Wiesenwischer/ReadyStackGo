using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Notifies the product of a maintenance transition via a signed webhook:
/// POST { "state": "maintenance" | "normal" } with an HMAC-SHA256 signature header.
/// Best-effort with a per-attempt timeout and bounded retries; the secret is never logged.
/// </summary>
public sealed class WebhookSetter : IMaintenanceSetter
{
    private readonly WebhookSetterSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookSetter> _logger;

    public WebhookSetter(
        MaintenanceSetterConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookSetter> logger)
    {
        _settings = config.Settings as WebhookSetterSettings
            ?? throw new ArgumentException("Invalid settings type for webhook setter");
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public SetterType Type => SetterType.Webhook;

    public async Task<SetterResult> SetAsync(MaintenanceState state, CancellationToken cancellationToken = default)
    {
        var stateValue = state == MaintenanceState.Maintenance ? "maintenance" : "normal";
        var body = $"{{\"state\":\"{stateValue}\"}}";

        var attempts = _settings.MaxRetries + 1;
        string? lastError = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _settings.Url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(_settings.Secret))
                {
                    request.Headers.TryAddWithoutValidation(
                        WebhookSignature.HeaderName, WebhookSignature.Compute(_settings.Secret, body));
                }

                var client = _httpClientFactory.CreateClient("MaintenanceSetter");

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_settings.Timeout);

                using var response = await client.SendAsync(request, timeoutCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Maintenance webhook notified product of state '{State}' (attempt {Attempt}/{Attempts})",
                        stateValue, attempt, attempts);
                    return SetterResult.Ok();
                }

                lastError = $"HTTP {(int)response.StatusCode}";
                _logger.LogWarning(
                    "Maintenance webhook returned {Status} (attempt {Attempt}/{Attempts})",
                    (int)response.StatusCode, attempt, attempts);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller cancelled — stop retrying.
                return SetterResult.Failed("Cancelled");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex,
                    "Maintenance webhook call failed (attempt {Attempt}/{Attempts})", attempt, attempts);
            }
        }

        return SetterResult.Failed($"Webhook failed after {attempts} attempt(s): {lastError}");
    }
}
