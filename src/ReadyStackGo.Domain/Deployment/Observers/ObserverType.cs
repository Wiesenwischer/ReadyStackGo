namespace ReadyStackGo.Domain.Deployment.Observers;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Type of maintenance observer.
/// Uses the Enumeration Class pattern for type safety and extensibility.
/// Only identifies the type - actual behavior is implemented in Infrastructure.
/// </summary>
public sealed class ObserverType : ValueObject
{
    public static readonly ObserverType SqlExtendedProperty = new("sqlExtendedProperty", "SQL Extended Property");
    public static readonly ObserverType SqlQuery = new("sqlQuery", "SQL Query");
    public static readonly ObserverType Http = new("http", "HTTP Endpoint");
    public static readonly ObserverType File = new("file", "File");

    private static readonly Dictionary<string, ObserverType> _byValue = new(StringComparer.OrdinalIgnoreCase)
    {
        { SqlExtendedProperty.Value, SqlExtendedProperty },
        { SqlQuery.Value, SqlQuery },
        { Http.Value, Http },
        { File.Value, File }
    };

    /// <summary>
    /// The value used in manifest YAML (e.g., "sqlExtendedProperty").
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; }

    private ObserverType(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    // For serialization
    private ObserverType() : this(string.Empty, string.Empty) { }

    /// <summary>
    /// Indicates if this observer type requires a database connection.
    /// </summary>
    public bool RequiresConnection => this == SqlExtendedProperty || this == SqlQuery;

    /// <summary>
    /// Indicates if this observer type uses HTTP.
    /// </summary>
    public bool RequiresUrl => this == Http;

    /// <summary>
    /// Indicates if this observer type monitors files.
    /// </summary>
    public bool RequiresFilePath => this == File;

    public static ObserverType FromValue(string value)
    {
        if (_byValue.TryGetValue(value, out var type))
            return type;
        throw new ArgumentException($"Unknown ObserverType: {value}");
    }

    public static bool TryFromValue(string value, out ObserverType? type)
    {
        return _byValue.TryGetValue(value, out type);
    }

    public static IEnumerable<ObserverType> GetAll()
    {
        yield return SqlExtendedProperty;
        yield return SqlQuery;
        yield return Http;
        yield return File;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
