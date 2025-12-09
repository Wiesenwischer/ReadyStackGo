namespace ReadyStackGo.Domain.Deployment.Observers;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Result of a maintenance observer check.
/// Immutable value object representing the observed state.
/// </summary>
public sealed class ObserverResult : ValueObject
{
    /// <summary>
    /// The observed value (e.g., "1", "true", "maintenance").
    /// </summary>
    public string? ObservedValue { get; }

    /// <summary>
    /// Whether maintenance mode should be active based on the observation.
    /// </summary>
    public bool IsMaintenanceRequired { get; }

    /// <summary>
    /// Whether the check was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Error message if the check failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Timestamp when the check was performed.
    /// </summary>
    public DateTimeOffset CheckedAt { get; }

    private ObserverResult(string? observedValue, bool isMaintenanceRequired, bool isSuccess, string? errorMessage, DateTimeOffset checkedAt)
    {
        ObservedValue = observedValue;
        IsMaintenanceRequired = isMaintenanceRequired;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        CheckedAt = checkedAt;
    }

    /// <summary>
    /// Creates a successful result indicating maintenance is required.
    /// </summary>
    public static ObserverResult MaintenanceRequired(string observedValue)
        => new(observedValue, isMaintenanceRequired: true, isSuccess: true, errorMessage: null, DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a successful result indicating normal operation.
    /// </summary>
    public static ObserverResult NormalOperation(string observedValue)
        => new(observedValue, isMaintenanceRequired: false, isSuccess: true, errorMessage: null, DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a failed result due to an error during the check.
    /// </summary>
    public static ObserverResult Failed(string errorMessage)
        => new(observedValue: null, isMaintenanceRequired: false, isSuccess: false, errorMessage, DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a result based on whether the observed value matches the maintenance value.
    /// </summary>
    public static ObserverResult FromComparison(string observedValue, string maintenanceValue, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var isMaintenanceRequired = string.Equals(observedValue, maintenanceValue, comparison);
        return new(observedValue, isMaintenanceRequired, isSuccess: true, errorMessage: null, DateTimeOffset.UtcNow);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ObservedValue;
        yield return IsMaintenanceRequired;
        yield return IsSuccess;
        yield return ErrorMessage;
        yield return CheckedAt;
    }

    public override string ToString()
    {
        if (!IsSuccess)
            return $"Failed: {ErrorMessage}";

        return IsMaintenanceRequired
            ? $"Maintenance required (value: {ObservedValue})"
            : $"Normal operation (value: {ObservedValue})";
    }
}
