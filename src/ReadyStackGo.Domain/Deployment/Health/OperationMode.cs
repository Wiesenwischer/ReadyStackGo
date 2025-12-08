namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Operation mode of a stack deployment.
/// Controlled by RSGO, not by the containers.
/// Uses the Enumeration Class pattern to encapsulate behavior.
/// </summary>
public sealed class OperationMode : ValueObject
{
    public static readonly OperationMode Normal = new(0, "Normal", "Normal operation, no planned restrictions", isPlannedRestriction: false);
    public static readonly OperationMode Migrating = new(1, "Migrating", "Planned migration/upgrade is running", isPlannedRestriction: true);
    public static readonly OperationMode Maintenance = new(2, "Maintenance", "Stack is in planned maintenance mode", isPlannedRestriction: true);
    public static readonly OperationMode Stopped = new(3, "Stopped", "Stack is intentionally stopped", isPlannedRestriction: true);
    public static readonly OperationMode Failed = new(4, "Failed", "Last operation failed, manual intervention required", isPlannedRestriction: false);

    private static readonly Dictionary<int, OperationMode> _byValue = new()
    {
        { Normal.Value, Normal },
        { Migrating.Value, Migrating },
        { Maintenance.Value, Maintenance },
        { Stopped.Value, Stopped },
        { Failed.Value, Failed }
    };

    private static readonly Dictionary<string, OperationMode> _byName = new(StringComparer.OrdinalIgnoreCase)
    {
        { Normal.Name, Normal },
        { Migrating.Name, Migrating },
        { Maintenance.Name, Maintenance },
        { Stopped.Name, Stopped },
        { Failed.Name, Failed }
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
    /// Description of the mode.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Indicates if this is a planned restriction (expected degradation).
    /// </summary>
    public bool IsPlannedRestriction { get; }

    private OperationMode(int value, string name, string description, bool isPlannedRestriction)
    {
        Value = value;
        Name = name;
        Description = description;
        IsPlannedRestriction = isPlannedRestriction;
    }

    // For EF Core
    private OperationMode() : this(0, string.Empty, string.Empty, false) { }

    /// <summary>
    /// Indicates if the stack is available for normal operations.
    /// </summary>
    public bool IsAvailable => this == Normal;

    /// <summary>
    /// Indicates if deployments are allowed in this mode.
    /// </summary>
    public bool AllowsDeployment => this == Normal || this == Failed;

    /// <summary>
    /// Indicates if the stack can be started.
    /// </summary>
    public bool CanStart => this == Stopped || this == Failed;

    /// <summary>
    /// Indicates if the stack can be stopped.
    /// </summary>
    public bool CanStop => this == Normal || this == Migrating || this == Maintenance;

    /// <summary>
    /// Indicates if the mode requires user attention.
    /// </summary>
    public bool RequiresAttention => this == Failed;

    /// <summary>
    /// Indicates if health degradation is expected in this mode.
    /// </summary>
    public bool ExpectsDegradedHealth => this == Migrating || this == Maintenance || this == Stopped;

    /// <summary>
    /// Returns the minimum overall health status for this operation mode.
    /// </summary>
    public HealthStatus MinimumHealthStatus => this switch
    {
        _ when this == Normal => HealthStatus.Healthy,
        _ when this == Migrating => HealthStatus.Degraded,
        _ when this == Maintenance => HealthStatus.Degraded,
        _ when this == Stopped => HealthStatus.Degraded,
        _ when this == Failed => HealthStatus.Unhealthy,
        _ => HealthStatus.Unknown
    };

    /// <summary>
    /// Gets the valid transitions from this mode.
    /// </summary>
    public IEnumerable<OperationMode> GetValidTransitions()
    {
        return this switch
        {
            _ when this == Normal => new[] { Migrating, Maintenance, Stopped },
            _ when this == Migrating => new[] { Normal, Failed },
            _ when this == Maintenance => new[] { Normal },
            _ when this == Stopped => new[] { Normal },
            _ when this == Failed => new[] { Normal, Migrating },
            _ => Enumerable.Empty<OperationMode>()
        };
    }

    /// <summary>
    /// Checks if transition to the target mode is valid.
    /// </summary>
    public bool CanTransitionTo(OperationMode target)
    {
        return GetValidTransitions().Contains(target);
    }

    public static OperationMode FromValue(int value)
    {
        if (_byValue.TryGetValue(value, out var mode))
            return mode;
        throw new ArgumentException($"Unknown OperationMode value: {value}");
    }

    public static OperationMode FromName(string name)
    {
        if (_byName.TryGetValue(name, out var mode))
            return mode;
        throw new ArgumentException($"Unknown OperationMode name: {name}");
    }

    public static bool TryFromValue(int value, out OperationMode? mode)
    {
        return _byValue.TryGetValue(value, out mode);
    }

    public static bool TryFromName(string name, out OperationMode? mode)
    {
        return _byName.TryGetValue(name, out mode);
    }

    public static IEnumerable<OperationMode> GetAll()
    {
        yield return Normal;
        yield return Migrating;
        yield return Maintenance;
        yield return Stopped;
        yield return Failed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Name;

    // Implicit conversion to int for persistence
    public static implicit operator int(OperationMode mode) => mode.Value;
}
