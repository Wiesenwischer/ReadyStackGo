namespace ReadyStackGo.Domain.Deployment.Observers;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Configuration for a maintenance observer.
/// Value object containing all settings needed to create an observer instance.
/// </summary>
public sealed class MaintenanceObserverConfig : ValueObject
{
    /// <summary>
    /// Type of observer (determines which implementation to use).
    /// </summary>
    public ObserverType Type { get; }

    /// <summary>
    /// Polling interval between checks.
    /// </summary>
    public TimeSpan PollingInterval { get; }

    /// <summary>
    /// Value that indicates maintenance mode is active.
    /// </summary>
    public string MaintenanceValue { get; }

    /// <summary>
    /// Value that indicates normal operation (optional, used for explicit confirmation).
    /// </summary>
    public string? NormalValue { get; }

    /// <summary>
    /// Type-specific settings (connection string, URL, file path, etc.).
    /// </summary>
    public IObserverSettings Settings { get; }

    private MaintenanceObserverConfig(
        ObserverType type,
        TimeSpan pollingInterval,
        string maintenanceValue,
        string? normalValue,
        IObserverSettings settings)
    {
        Type = type;
        PollingInterval = pollingInterval;
        MaintenanceValue = maintenanceValue;
        NormalValue = normalValue;
        Settings = settings;
    }

    public static MaintenanceObserverConfig Create(
        ObserverType type,
        TimeSpan pollingInterval,
        string maintenanceValue,
        string? normalValue,
        IObserverSettings settings)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(settings);

        if (pollingInterval <= TimeSpan.Zero)
            throw new ArgumentException("Polling interval must be positive", nameof(pollingInterval));

        if (string.IsNullOrWhiteSpace(maintenanceValue))
            throw new ArgumentException("Maintenance value cannot be empty", nameof(maintenanceValue));

        return new MaintenanceObserverConfig(type, pollingInterval, maintenanceValue, normalValue, settings);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return PollingInterval;
        yield return MaintenanceValue;
        yield return NormalValue;
        yield return Settings;
    }
}

/// <summary>
/// Marker interface for type-specific observer settings.
/// Each observer type has its own settings implementation.
/// </summary>
public interface IObserverSettings
{
    /// <summary>
    /// Validates the settings and returns any validation errors.
    /// </summary>
    IEnumerable<string> Validate();
}

/// <summary>
/// Settings for SQL-based observers (Extended Property and Query).
/// </summary>
public sealed class SqlObserverSettings : ValueObject, IObserverSettings
{
    /// <summary>
    /// Direct connection string value.
    /// </summary>
    public string? ConnectionString { get; }

    /// <summary>
    /// Name of a variable containing the connection string (alternative to ConnectionString).
    /// </summary>
    public string? ConnectionName { get; }

    /// <summary>
    /// For SqlExtendedProperty: Name of the extended property to read.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// For SqlQuery: The SQL query to execute (must return a single scalar value).
    /// </summary>
    public string? Query { get; }

    private SqlObserverSettings(string? connectionString, string? connectionName, string? propertyName, string? query)
    {
        ConnectionString = connectionString;
        ConnectionName = connectionName;
        PropertyName = propertyName;
        Query = query;
    }

    public static SqlObserverSettings ForExtendedProperty(string propertyName, string? connectionString = null, string? connectionName = null)
        => new(connectionString, connectionName, propertyName, query: null);

    public static SqlObserverSettings ForQuery(string query, string? connectionString = null, string? connectionName = null)
        => new(connectionString, connectionName, propertyName: null, query);

    public bool HasConnection => !string.IsNullOrEmpty(ConnectionString) || !string.IsNullOrEmpty(ConnectionName);

    public IEnumerable<string> Validate()
    {
        if (!HasConnection)
            yield return "Either connectionString or connectionName must be specified";

        if (!string.IsNullOrEmpty(ConnectionString) && !string.IsNullOrEmpty(ConnectionName))
            yield return "Specify either connectionString or connectionName, not both";

        if (string.IsNullOrEmpty(PropertyName) && string.IsNullOrEmpty(Query))
            yield return "Either propertyName (for Extended Property) or query (for SQL Query) must be specified";

        if (!string.IsNullOrEmpty(PropertyName) && !string.IsNullOrEmpty(Query))
            yield return "Specify either propertyName or query, not both";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ConnectionString;
        yield return ConnectionName;
        yield return PropertyName;
        yield return Query;
    }
}

/// <summary>
/// Settings for HTTP observer.
/// </summary>
public sealed class HttpObserverSettings : ValueObject, IObserverSettings
{
    /// <summary>
    /// URL to call for maintenance state check.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// HTTP method to use (GET, POST, etc.). Defaults to GET.
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// Optional headers to include in the request.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; }

    /// <summary>
    /// Timeout for the HTTP request.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// JSONPath expression to extract the value from the response (optional).
    /// If not specified, the entire response body is used.
    /// </summary>
    public string? JsonPath { get; }

    private HttpObserverSettings(string url, string method, IReadOnlyDictionary<string, string>? headers, TimeSpan timeout, string? jsonPath)
    {
        Url = url;
        Method = method;
        Headers = headers;
        Timeout = timeout;
        JsonPath = jsonPath;
    }

    public static HttpObserverSettings Create(
        string url,
        string method = "GET",
        IReadOnlyDictionary<string, string>? headers = null,
        TimeSpan? timeout = null,
        string? jsonPath = null)
    {
        return new HttpObserverSettings(
            url,
            method,
            headers,
            timeout ?? TimeSpan.FromSeconds(10),
            jsonPath);
    }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
            yield return "URL must be specified";

        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            yield return "URL must be a valid HTTP or HTTPS URL";

        if (Timeout <= TimeSpan.Zero)
            yield return "Timeout must be positive";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Url;
        yield return Method;
        yield return Timeout;
        yield return JsonPath;
        // Note: Headers comparison simplified
        yield return Headers?.Count ?? 0;
    }
}

/// <summary>
/// Settings for File observer.
/// </summary>
public sealed class FileObserverSettings : ValueObject, IObserverSettings
{
    /// <summary>
    /// Path to the file to monitor.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Check mode: "exists" (file existence) or "content" (file content).
    /// </summary>
    public FileCheckMode Mode { get; }

    /// <summary>
    /// For content mode: regex pattern to extract the value from file content.
    /// </summary>
    public string? ContentPattern { get; }

    private FileObserverSettings(string path, FileCheckMode mode, string? contentPattern)
    {
        Path = path;
        Mode = mode;
        ContentPattern = contentPattern;
    }

    public static FileObserverSettings ForExistence(string path)
        => new(path, FileCheckMode.Exists, contentPattern: null);

    public static FileObserverSettings ForContent(string path, string? contentPattern = null)
        => new(path, FileCheckMode.Content, contentPattern);

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Path))
            yield return "File path must be specified";

        if (Mode == FileCheckMode.Content && !string.IsNullOrEmpty(ContentPattern))
        {
            // Validate regex pattern
            var isValidPattern = true;
            try
            {
                _ = new System.Text.RegularExpressions.Regex(ContentPattern);
            }
            catch
            {
                isValidPattern = false;
            }

            if (!isValidPattern)
                yield return "Content pattern is not a valid regular expression";
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Path;
        yield return Mode;
        yield return ContentPattern;
    }
}

/// <summary>
/// File check mode for file observer.
/// </summary>
public enum FileCheckMode
{
    /// <summary>
    /// Check if file exists. Returns "true" if exists, "false" otherwise.
    /// </summary>
    Exists,

    /// <summary>
    /// Read file content and optionally extract value using pattern.
    /// </summary>
    Content
}
