using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services.Health;

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
            List<HealthCheckEntryResult>? entries = null;

            try
            {
                var content = await response.Content.ReadAsStringAsync(cts.Token);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var parsed = ParseHealthResponse(content);
                    reportedStatus = parsed.Status;
                    entries = parsed.Entries;
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
                    Entries = entries
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
                    Entries = entries
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
    /// Supports both simple string responses and JSON responses with full entry details.
    /// </summary>
    private (string? Status, List<HealthCheckEntryResult>? Entries) ParseHealthResponse(string content)
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

                // Try to extract individual check results with full details
                List<HealthCheckEntryResult>? entries = null;
                if (json.TryGetProperty("entries", out var entriesElement) ||
                    json.TryGetProperty("Entries", out entriesElement) ||
                    json.TryGetProperty("results", out entriesElement) ||
                    json.TryGetProperty("Results", out entriesElement))
                {
                    if (entriesElement.ValueKind == JsonValueKind.Object)
                    {
                        entries = new List<HealthCheckEntryResult>();
                        foreach (var entry in entriesElement.EnumerateObject())
                        {
                            entries.Add(ParseHealthCheckEntry(entry.Name, entry.Value));
                        }
                    }
                }

                return (status, entries);
            }
            catch (JsonException)
            {
                // Not valid JSON, return null
            }
        }

        return (null, null);
    }

    private static HealthCheckEntryResult ParseHealthCheckEntry(string name, JsonElement entry)
    {
        var entryStatus = "Unknown";
        if (entry.TryGetProperty("status", out var statusProp) ||
            entry.TryGetProperty("Status", out statusProp))
        {
            entryStatus = statusProp.GetString() ?? "Unknown";
        }

        string? description = null;
        if (entry.TryGetProperty("description", out var descProp) ||
            entry.TryGetProperty("Description", out descProp))
        {
            description = descProp.ValueKind == JsonValueKind.String ? descProp.GetString() : null;
        }

        double? durationMs = null;
        if (entry.TryGetProperty("duration", out var durationProp) ||
            entry.TryGetProperty("Duration", out durationProp))
        {
            durationMs = ParseDurationToMs(durationProp);
        }

        Dictionary<string, string>? data = null;
        if (entry.TryGetProperty("data", out var dataProp) ||
            entry.TryGetProperty("Data", out dataProp))
        {
            if (dataProp.ValueKind == JsonValueKind.Object)
            {
                data = new Dictionary<string, string>();
                foreach (var kvp in dataProp.EnumerateObject())
                {
                    data[kvp.Name] = kvp.Value.ToString();
                }
            }
        }

        List<string>? tags = null;
        if (entry.TryGetProperty("tags", out var tagsProp) ||
            entry.TryGetProperty("Tags", out tagsProp))
        {
            if (tagsProp.ValueKind == JsonValueKind.Array)
            {
                tags = new List<string>();
                foreach (var tag in tagsProp.EnumerateArray())
                {
                    var tagStr = tag.GetString();
                    if (tagStr != null)
                        tags.Add(tagStr);
                }
            }
        }

        string? exception = null;
        if (entry.TryGetProperty("exception", out var exProp) ||
            entry.TryGetProperty("Exception", out exProp))
        {
            exception = exProp.ValueKind == JsonValueKind.String ? exProp.GetString() : null;
        }

        return new HealthCheckEntryResult
        {
            Name = name,
            Status = entryStatus,
            Description = description,
            DurationMs = durationMs,
            Data = data,
            Tags = tags,
            Exception = exception
        };
    }

    private static double? ParseDurationToMs(JsonElement durationProp)
    {
        if (durationProp.ValueKind == JsonValueKind.String)
        {
            var durationStr = durationProp.GetString();
            if (durationStr != null && TimeSpan.TryParse(durationStr, out var ts))
                return ts.TotalMilliseconds;
        }
        else if (durationProp.ValueKind == JsonValueKind.Number)
        {
            return durationProp.GetDouble();
        }

        return null;
    }
}
