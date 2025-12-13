namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Maintenance observer configuration.
/// Monitors external systems to automatically trigger maintenance mode.
/// </summary>
public class RsgoMaintenanceObserver
{
    /// <summary>
    /// Observer type: "sqlExtendedProperty", "sqlQuery", "http", or "file".
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Polling interval (e.g., "30s", "1m", "5m").
    /// </summary>
    public string PollingInterval { get; set; } = "30s";

    /// <summary>
    /// Value that indicates maintenance mode is active.
    /// </summary>
    public required string MaintenanceValue { get; set; }

    /// <summary>
    /// Value that indicates normal operation (optional).
    /// If not specified, any value other than maintenanceValue is treated as normal.
    /// </summary>
    public string? NormalValue { get; set; }

    #region SQL Observer Settings (sqlExtendedProperty, sqlQuery)

    /// <summary>
    /// Direct connection string value.
    /// Supports variable substitution: ${VAR_NAME}
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Name of a manifest variable containing the connection string.
    /// Alternative to connectionString - use the variable name directly.
    /// </summary>
    public string? ConnectionName { get; set; }

    /// <summary>
    /// For sqlExtendedProperty: Name of the SQL Server extended property to read.
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// For sqlQuery: SQL query that returns a single scalar value.
    /// </summary>
    public string? Query { get; set; }

    #endregion

    #region HTTP Observer Settings

    /// <summary>
    /// For http: URL to call for maintenance state check.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// For http: HTTP method (GET, POST, etc.). Defaults to GET.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// For http: Optional headers to include in the request.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// For http: Request timeout (e.g., "10s"). Defaults to 10s.
    /// </summary>
    public string? Timeout { get; set; }

    /// <summary>
    /// For http: JSONPath expression to extract the value from response.
    /// If not specified, the entire response body is used.
    /// </summary>
    public string? JsonPath { get; set; }

    #endregion

    #region File Observer Settings

    /// <summary>
    /// For file: Path to the file to monitor.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// For file: Check mode - "exists" or "content".
    /// - exists: Returns "true" if file exists, "false" otherwise
    /// - content: Reads file content (optionally with pattern extraction)
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// For file (content mode): Regex pattern to extract value from file content.
    /// First capture group is used, or entire match if no groups.
    /// </summary>
    public string? ContentPattern { get; set; }

    #endregion
}
