namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Health status of NServiceBus-based messaging for a stack.
/// Used primarily for first-party stacks with NServiceBus integration.
/// </summary>
public sealed class BusHealth : ValueObject
{
    private readonly List<BusEndpointHealth> _endpoints;

    /// <summary>
    /// Overall bus health status.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Transport key identifier (e.g., "primary-sql").
    /// </summary>
    public string? TransportKey { get; }

    /// <summary>
    /// Indicates if a critical error has occurred.
    /// </summary>
    public bool HasCriticalError { get; }

    /// <summary>
    /// Critical error message if any.
    /// </summary>
    public string? CriticalErrorMessage { get; }

    /// <summary>
    /// Timestamp of last processed health ping.
    /// </summary>
    public DateTime? LastHealthPingProcessedUtc { get; }

    /// <summary>
    /// Time since last health ping.
    /// </summary>
    public TimeSpan? TimeSinceLastPing { get; }

    /// <summary>
    /// Configuration value for when ping is considered too old.
    /// </summary>
    public TimeSpan? UnhealthyAfter { get; }

    /// <summary>
    /// Health status of individual bus endpoints.
    /// </summary>
    public IReadOnlyList<BusEndpointHealth> Endpoints => _endpoints.AsReadOnly();

    private BusHealth(
        HealthStatus status,
        string? transportKey,
        bool hasCriticalError,
        string? criticalErrorMessage,
        DateTime? lastHealthPingProcessedUtc,
        TimeSpan? timeSinceLastPing,
        TimeSpan? unhealthyAfter,
        IEnumerable<BusEndpointHealth>? endpoints)
    {
        Status = status;
        TransportKey = transportKey;
        HasCriticalError = hasCriticalError;
        CriticalErrorMessage = criticalErrorMessage;
        LastHealthPingProcessedUtc = lastHealthPingProcessedUtc;
        TimeSinceLastPing = timeSinceLastPing;
        UnhealthyAfter = unhealthyAfter;
        _endpoints = endpoints?.ToList() ?? new List<BusEndpointHealth>();
    }

    public static BusHealth Create(
        HealthStatus status,
        string? transportKey = null,
        bool hasCriticalError = false,
        string? criticalErrorMessage = null,
        DateTime? lastHealthPingProcessedUtc = null,
        TimeSpan? timeSinceLastPing = null,
        TimeSpan? unhealthyAfter = null,
        IEnumerable<BusEndpointHealth>? endpoints = null)
    {
        return new BusHealth(
            status,
            transportKey,
            hasCriticalError,
            criticalErrorMessage,
            lastHealthPingProcessedUtc,
            timeSinceLastPing,
            unhealthyAfter,
            endpoints);
    }

    public static BusHealth Healthy(string? transportKey = null)
    {
        return new BusHealth(
            HealthStatus.Healthy,
            transportKey,
            false,
            null,
            SystemClock.UtcNow,
            TimeSpan.Zero,
            null,
            null);
    }

    public static BusHealth Unknown()
    {
        return new BusHealth(
            HealthStatus.Unknown,
            null,
            false,
            null,
            null,
            null,
            null,
            null);
    }

    public static BusHealth NotApplicable()
    {
        // For stacks that don't use NServiceBus
        return Unknown();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Status;
        yield return TransportKey;
        yield return HasCriticalError;
        yield return CriticalErrorMessage;
        yield return LastHealthPingProcessedUtc;
        yield return TimeSinceLastPing;
        yield return UnhealthyAfter;
        foreach (var endpoint in _endpoints.OrderBy(e => e.EndpointName))
        {
            yield return endpoint;
        }
    }
}
