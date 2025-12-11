namespace ReadyStackGo.Domain.Deployment.RuntimeConfig;

/// <summary>
/// Configuration for maintenance mode detection via an observer.
/// This is runtime configuration applied when a stack is deployed.
/// </summary>
public record MaintenanceObserverConfig
{
    /// <summary>
    /// Observer type: "sqlExtendedProperty", "sqlQuery", "http", or "file".
    /// </summary>
    public string Type { get; init; } = null!;

    /// <summary>
    /// Polling interval (e.g., "30s", "1m", "5m").
    /// </summary>
    public string PollingInterval { get; init; } = "30s";

    /// <summary>
    /// Value that indicates maintenance mode is active.
    /// </summary>
    public string MaintenanceValue { get; init; } = null!;

    /// <summary>
    /// Value that indicates normal operation (optional).
    /// </summary>
    public string? NormalValue { get; init; }

    #region SQL Observer Settings

    /// <summary>
    /// Connection string for SQL observers.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Name of a variable containing the connection string.
    /// </summary>
    public string? ConnectionName { get; init; }

    /// <summary>
    /// For sqlExtendedProperty: Name of the property to read.
    /// </summary>
    public string? PropertyName { get; init; }

    /// <summary>
    /// For sqlQuery: SQL query returning a scalar value.
    /// </summary>
    public string? Query { get; init; }

    #endregion

    #region HTTP Observer Settings

    /// <summary>
    /// URL to call for maintenance state check.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// HTTP method (GET, POST, etc.).
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Optional headers to include.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Request timeout (e.g., "10s").
    /// </summary>
    public string? Timeout { get; init; }

    /// <summary>
    /// JSONPath expression to extract value from response.
    /// </summary>
    public string? JsonPath { get; init; }

    #endregion

    #region File Observer Settings

    /// <summary>
    /// Path to the file to monitor.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Check mode: "exists" or "content".
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>
    /// Regex pattern to extract value from file content.
    /// </summary>
    public string? ContentPattern { get; init; }

    #endregion

    public MaintenanceObserverConfig() { }

    public MaintenanceObserverConfig(
        string type,
        string maintenanceValue,
        string pollingInterval = "30s",
        string? normalValue = null)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        MaintenanceValue = maintenanceValue ?? throw new ArgumentNullException(nameof(maintenanceValue));
        PollingInterval = pollingInterval;
        NormalValue = normalValue;
    }
}
