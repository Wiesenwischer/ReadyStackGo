namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Technical health status of a component.
/// Uses the Enumeration Class pattern to encapsulate behavior.
/// </summary>
public sealed class HealthStatus : ValueObject
{
    public static readonly HealthStatus Healthy = new(0, "Healthy", "Component is fully operational", Severity.None);
    public static readonly HealthStatus Degraded = new(1, "Degraded", "Component is operational but with issues", Severity.Warning);
    public static readonly HealthStatus Unhealthy = new(2, "Unhealthy", "Component is not operational", Severity.Critical);
    public static readonly HealthStatus Unknown = new(3, "Unknown", "Health status could not be determined", Severity.Info);

    private static readonly Dictionary<int, HealthStatus> _byValue = new()
    {
        { Healthy.Value, Healthy },
        { Degraded.Value, Degraded },
        { Unhealthy.Value, Unhealthy },
        { Unknown.Value, Unknown }
    };

    private static readonly Dictionary<string, HealthStatus> _byName = new(StringComparer.OrdinalIgnoreCase)
    {
        { Healthy.Name, Healthy },
        { Degraded.Name, Degraded },
        { Unhealthy.Name, Unhealthy },
        { Unknown.Name, Unknown }
    };

    /// <summary>
    /// Numeric value for persistence.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Description of the status.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Severity level for alerting/display.
    /// </summary>
    public Severity SeverityLevel { get; }

    private HealthStatus(int value, string name, string description, Severity severity)
    {
        Value = value;
        Name = name;
        Description = description;
        SeverityLevel = severity;
    }

    // For EF Core
    private HealthStatus() : this(0, string.Empty, string.Empty, Severity.None) { }

    /// <summary>
    /// Indicates if the status represents an operational state.
    /// </summary>
    public bool IsOperational => this == Healthy || this == Degraded;

    /// <summary>
    /// Indicates if the status requires attention.
    /// </summary>
    public bool RequiresAttention => this == Degraded || this == Unhealthy;

    /// <summary>
    /// Indicates if the status is critical.
    /// </summary>
    public bool IsCritical => this == Unhealthy;

    /// <summary>
    /// Indicates if actions (like start/stop) are allowed in this state.
    /// </summary>
    public bool AllowsActions => this != Unknown;

    /// <summary>
    /// Combines two health statuses, returning the worse one.
    /// </summary>
    public HealthStatus CombineWith(HealthStatus other)
    {
        if (other == null) return this;
        return Value > other.Value ? this : other;
    }

    /// <summary>
    /// Gets the worst status from a collection.
    /// </summary>
    public static HealthStatus Aggregate(IEnumerable<HealthStatus> statuses)
    {
        var list = statuses.ToList();
        if (!list.Any()) return Unknown;
        return list.Aggregate((current, next) => current.CombineWith(next));
    }

    public static HealthStatus FromValue(int value)
    {
        if (_byValue.TryGetValue(value, out var status))
            return status;
        throw new ArgumentException($"Unknown HealthStatus value: {value}");
    }

    public static HealthStatus FromName(string name)
    {
        if (_byName.TryGetValue(name, out var status))
            return status;
        throw new ArgumentException($"Unknown HealthStatus name: {name}");
    }

    public static bool TryFromValue(int value, out HealthStatus? status)
    {
        return _byValue.TryGetValue(value, out status);
    }

    public static bool TryFromName(string name, out HealthStatus? status)
    {
        return _byName.TryGetValue(name, out status);
    }

    public static IEnumerable<HealthStatus> GetAll()
    {
        yield return Healthy;
        yield return Degraded;
        yield return Unhealthy;
        yield return Unknown;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Name;

    // Implicit conversion to int for persistence
    public static implicit operator int(HealthStatus status) => status.Value;
}

/// <summary>
/// Severity level for alerting and display purposes.
/// </summary>
public enum Severity
{
    None = 0,
    Info = 1,
    Warning = 2,
    Critical = 3
}
