namespace ReadyStackGo.Domain.Deployment.Observers;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object that tracks who triggered the maintenance mode.
/// Used to enforce ownership rules: only the source that activated maintenance can deactivate it.
/// </summary>
public sealed class MaintenanceTrigger : ValueObject
{
    /// <summary>
    /// Who triggered the maintenance mode (Manual or Observer).
    /// </summary>
    public MaintenanceTriggerSource Source { get; }

    /// <summary>
    /// Optional reason for the maintenance mode change.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// When the maintenance mode was triggered.
    /// </summary>
    public DateTime TriggeredAtUtc { get; }

    /// <summary>
    /// Who triggered the change (user ID for Manual, observer type for Observer).
    /// </summary>
    public string? TriggeredBy { get; }

    private MaintenanceTrigger(
        MaintenanceTriggerSource source,
        string? reason,
        DateTime triggeredAtUtc,
        string? triggeredBy)
    {
        Source = source;
        Reason = reason;
        TriggeredAtUtc = triggeredAtUtc;
        TriggeredBy = triggeredBy;
    }

    /// <summary>
    /// Creates a trigger for a manual maintenance mode change (UI or API).
    /// </summary>
    public static MaintenanceTrigger Manual(string? reason = null, string? triggeredBy = null)
    {
        return new MaintenanceTrigger(
            MaintenanceTriggerSource.Manual,
            reason,
            SystemClock.UtcNow,
            triggeredBy);
    }

    /// <summary>
    /// Creates a trigger for an observer-initiated maintenance mode change.
    /// </summary>
    public static MaintenanceTrigger Observer(string? reason = null, string? observerType = null)
    {
        return new MaintenanceTrigger(
            MaintenanceTriggerSource.Observer,
            reason,
            SystemClock.UtcNow,
            observerType);
    }

    /// <summary>
    /// Creates a trigger with an explicit timestamp (for deserialization).
    /// </summary>
    public static MaintenanceTrigger Create(
        MaintenanceTriggerSource source,
        string? reason,
        DateTime triggeredAtUtc,
        string? triggeredBy)
    {
        return new MaintenanceTrigger(source, reason, triggeredAtUtc, triggeredBy);
    }

    public bool IsManual => Source == MaintenanceTriggerSource.Manual;
    public bool IsObserver => Source == MaintenanceTriggerSource.Observer;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Source;
        yield return Reason;
        yield return TriggeredAtUtc;
        yield return TriggeredBy;
    }

    public override string ToString() =>
        $"MaintenanceTrigger [source={Source}, reason={Reason}, at={TriggeredAtUtc:u}, by={TriggeredBy}]";
}

/// <summary>
/// Source that triggered the maintenance mode change.
/// </summary>
public enum MaintenanceTriggerSource
{
    /// <summary>
    /// Manually triggered by a user via UI or API (including hooks).
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Automatically triggered by a maintenance observer polling an external source.
    /// </summary>
    Observer = 1
}
