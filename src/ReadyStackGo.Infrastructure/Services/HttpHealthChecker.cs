using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// HTTP-based health checker that calls container health endpoints directly.
/// Supports ASP.NET Core health check responses with status parsing.
/// </summary>
public class HttpHealthChecker : IHttpHealthChecker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpHealthChecker> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpHealthChecker(
        HttpClient httpClient,
        ILogger<HttpHealthChecker> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<HttpHealthCheckResult> CheckHealthAsync(
        string containerAddress,
        HttpHealthCheckConfig config,
        CancellationToken cancellationToken = default)
    {
        var scheme = config.UseHttps ? "https" : "http";
        var url = $"{scheme}://{containerAddress}:{config.Port}{config.Path}";

        _logger.LogDebug("Checking health at {Url}", url);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(config.Timeout);

            var response = await _httpClient.GetAsync(url, cts.Token);
            stopwatch.Stop();

            var responseTimeMs = (int)stopwatch.ElapsedMilliseconds;
            var statusCode = (int)response.StatusCode;

            // Check if status code is in healthy list
            var isHealthyStatusCode = config.HealthyStatusCodes.Contains(statusCode);

            // Try to parse ASP.NET Core health response
            string? reportedStatus = null;
            Dictionary<string, string>? details = null;

            try
            {
                var content = await response.Content.ReadAsStringAsync(cts.Token);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var parsed = ParseHealthResponse(content);
                    reportedStatus = parsed.Status;
                    details = parsed.Details;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not parse health response body");
            }

            // Determine final health status
            // If we got a parseable status, use it; otherwise fall back to status code check
            var isHealthy = reportedStatus != null
                ? reportedStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase)
                : isHealthyStatusCode;

            if (isHealthy)
            {
                _logger.LogDebug("Health check passed for {Url}: {StatusCode} in {ResponseTime}ms",
                    url, statusCode, responseTimeMs);

                return new HttpHealthCheckResult
                {
                    IsHealthy = true,
                    StatusCode = statusCode,
                    ResponseTimeMs = responseTimeMs,
                    ReportedStatus = reportedStatus ?? "Healthy",
                    Details = details
                };
            }
            else
            {
                var error = reportedStatus != null
                    ? $"Reported status: {reportedStatus}"
                    : $"Unexpected status code: {statusCode}";

                _logger.LogWarning("Health check failed for {Url}: {Error}", url, error);

                return new HttpHealthCheckResult
                {
                    IsHealthy = false,
                    StatusCode = statusCode,
                    ResponseTimeMs = responseTimeMs,
                    Error = error,
                    ReportedStatus = reportedStatus ?? "Unhealthy",
                    Details = details
                };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var error = $"Health check timed out after {config.Timeout.TotalSeconds}s";
            _logger.LogWarning("Health check timeout for {Url}", url);

            return HttpHealthCheckResult.ConnectionFailed(error);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var error = $"Connection failed: {ex.Message}";
            _logger.LogWarning(ex, "Health check connection failed for {Url}", url);

            return HttpHealthCheckResult.ConnectionFailed(error);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var error = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during health check for {Url}", url);

            return HttpHealthCheckResult.ConnectionFailed(error);
        }
    }

    /// <summary>
    /// Parses ASP.NET Core health check response format.
    /// Supports both simple string responses and JSON responses.
    /// </summary>
    private (string? Status, Dictionary<string, string>? Details) ParseHealthResponse(string content)
    {
        content = content.Trim();

        // Simple string response (e.g., "Healthy", "Unhealthy", "Degraded")
        if (content.Equals("Healthy", StringComparison.OrdinalIgnoreCase) ||
            content.Equals("Unhealthy", StringComparison.OrdinalIgnoreCase) ||
            content.Equals("Degraded", StringComparison.OrdinalIgnoreCase))
        {
            return (content, null);
        }

        // Try JSON response
        if (content.StartsWith("{") || content.StartsWith("["))
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

                // Try to find status field (various naming conventions)
                string? status = null;
                if (json.TryGetProperty("status", out var statusProp) ||
                    json.TryGetProperty("Status", out statusProp))
                {
                    status = statusProp.GetString();
                }

                // Try to extract individual check results
                Dictionary<string, string>? details = null;
                if (json.TryGetProperty("entries", out var entries) ||
                    json.TryGetProperty("Entries", out entries) ||
                    json.TryGetProperty("results", out entries) ||
                    json.TryGetProperty("Results", out entries))
                {
                    details = new Dictionary<string, string>();
                    if (entries.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var entry in entries.EnumerateObject())
                        {
                            var entryStatus = "Unknown";
                            if (entry.Value.TryGetProperty("status", out var entryStatusProp) ||
                                entry.Value.TryGetProperty("Status", out entryStatusProp))
                            {
                                entryStatus = entryStatusProp.GetString() ?? "Unknown";
                            }
                            details[entry.Name] = entryStatus;
                        }
                    }
                }

                return (status, details);
            }
            catch (JsonException)
            {
                // Not valid JSON, return null
            }
        }

        return (null, null);
    }
}
