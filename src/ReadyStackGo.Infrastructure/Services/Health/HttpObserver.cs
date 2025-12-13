using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Observer that calls an HTTP endpoint to determine maintenance state.
/// Supports JSON responses with optional JSONPath extraction.
/// </summary>
public sealed class HttpObserver : BaseMaintenanceObserver
{
    private readonly HttpObserverSettings _settings;
    private readonly HttpClient _httpClient;

    public HttpObserver(
        MaintenanceObserverConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<HttpObserver> logger)
        : base(config, logger)
    {
        _settings = config.Settings as HttpObserverSettings
            ?? throw new ArgumentException("Invalid settings type for HTTP observer");

        _httpClient = httpClientFactory.CreateClient("MaintenanceObserver");
        _httpClient.Timeout = _settings.Timeout;
    }

    public override ObserverType Type => ObserverType.Http;

    protected override async Task<string> GetObservedValueAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(_settings.Method),
            _settings.Url);

        // Add custom headers if specified
        if (_settings.Headers != null)
        {
            foreach (var (key, value) in _settings.Headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        // We don't require success status - the response body determines maintenance state
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrEmpty(_settings.JsonPath))
        {
            // Return entire response body (trimmed)
            return content.Trim();
        }

        // Extract value using JSONPath
        return ExtractJsonValue(content, _settings.JsonPath);
    }

    private string ExtractJsonValue(string json, string jsonPath)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var element = NavigateJsonPath(document.RootElement, jsonPath);

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => element.GetRawText()
            };
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse JSON response");
            throw new InvalidOperationException($"Failed to parse JSON response: {ex.Message}");
        }
    }

    private static JsonElement NavigateJsonPath(JsonElement element, string jsonPath)
    {
        // Simple JSONPath implementation supporting dot notation
        // e.g., "status.maintenance" or "data.state"
        var parts = jsonPath.TrimStart('$', '.').Split('.');

        var current = element;
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;

            // Handle array index notation like "items[0]"
            if (part.Contains('[') && part.Contains(']'))
            {
                var bracketStart = part.IndexOf('[');
                var propertyName = part[..bracketStart];
                var indexStr = part[(bracketStart + 1)..part.IndexOf(']')];

                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (!current.TryGetProperty(propertyName, out current))
                        throw new InvalidOperationException($"Property '{propertyName}' not found");
                }

                if (int.TryParse(indexStr, out var index))
                {
                    if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
                        throw new InvalidOperationException($"Array index {index} out of bounds");

                    current = current[index];
                }
            }
            else
            {
                if (!current.TryGetProperty(part, out current))
                    throw new InvalidOperationException($"Property '{part}' not found in JSON path '{jsonPath}'");
            }
        }

        return current;
    }
}
