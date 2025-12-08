namespace ReadyStackGo.Domain.Deployment.Health;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Health status of a single NServiceBus endpoint.
/// </summary>
public sealed class BusEndpointHealth : ValueObject
{
    /// <summary>
    /// Name of the endpoint.
    /// </summary>
    public string EndpointName { get; }

    /// <summary>
    /// Health status of the endpoint.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Last health ping timestamp.
    /// </summary>
    public DateTime? LastPingUtc { get; }

    /// <summary>
    /// Reason for current status (e.g., "NoMessagesRecently", "HeartbeatMissing").
    /// </summary>
    public string? Reason { get; }

    private BusEndpointHealth(
        string endpointName,
        HealthStatus status,
        DateTime? lastPingUtc,
        string? reason)
    {
        SelfAssertArgumentNotEmpty(endpointName, "Endpoint name cannot be empty.");

        EndpointName = endpointName;
        Status = status;
        LastPingUtc = lastPingUtc;
        Reason = reason;
    }

    public static BusEndpointHealth Create(
        string endpointName,
        HealthStatus status,
        DateTime? lastPingUtc = null,
        string? reason = null)
    {
        return new BusEndpointHealth(endpointName, status, lastPingUtc, reason);
    }

    public static BusEndpointHealth Healthy(string endpointName, DateTime lastPingUtc)
    {
        return new BusEndpointHealth(endpointName, HealthStatus.Healthy, lastPingUtc, null);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EndpointName;
        yield return Status;
        yield return LastPingUtc;
        yield return Reason;
    }
}
