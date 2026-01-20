namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Operation mode of a running deployment.
/// Only valid when DeploymentStatus == Running.
/// Controls runtime behavior, not deployment lifecycle.
/// </summary>
public sealed class OperationMode : ValueObject
{
    public static readonly OperationMode Normal = new(0, "Normal", "Normal operation");
    public static readonly OperationMode Maintenance = new(1, "Maintenance", "Stack is in maintenance mode, containers stopped");

    private static readonly Dictionary<int, OperationMode> _byValue = new()
    {
        { Normal.Value, Normal },
        { Maintenance.Value, Maintenance }
    };

    private static readonly Dictionary<string, OperationMode> _byName = new(StringComparer.OrdinalIgnoreCase)
    {
        { Normal.Name, Normal },
        { Maintenance.Name, Maintenance }
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

    private OperationMode(int value, string name, string description)
    {
        Value = value;
        Name = name;
        Description = description;
    }

    // For EF Core
    private OperationMode() : this(0, string.Empty, string.Empty) { }

    /// <summary>
    /// Indicates if the stack is available for normal operations.
    /// </summary>
    public bool IsAvailable => this == Normal;

    /// <summary>
    /// Indicates if health degradation is expected in this mode.
    /// </summary>
    public bool ExpectsDegradedHealth => this == Maintenance;

    /// <summary>
    /// Returns the minimum overall health status for this operation mode.
    /// </summary>
    public HealthStatus MinimumHealthStatus => this switch
    {
        _ when this == Normal => HealthStatus.Healthy,
        _ when this == Maintenance => HealthStatus.Degraded,
        _ => HealthStatus.Unknown
    };

    /// <summary>
    /// Gets the valid transitions from this mode.
    /// </summary>
    public IEnumerable<OperationMode> GetValidTransitions()
    {
        return this switch
        {
            _ when this == Normal => new[] { Maintenance },
            _ when this == Maintenance => new[] { Normal },
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
        yield return Maintenance;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Name;

    // Implicit conversion to int for persistence
    public static implicit operator int(OperationMode mode) => mode.Value;
}
