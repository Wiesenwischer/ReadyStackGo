namespace ReadyStackGo.Domain.Deployment.Observers;

/// <summary>
/// Propagates an RSGO-initiated maintenance state to the product (the write-side mirror of
/// <see cref="IMaintenanceObserver"/>). Implementations are best-effort: a failure is returned
/// as a non-successful <see cref="SetterResult"/>, never thrown, so it cannot break the
/// maintenance transition.
/// </summary>
public interface IMaintenanceSetter
{
    SetterType Type { get; }

    Task<SetterResult> SetAsync(MaintenanceState state, CancellationToken cancellationToken = default);
}

/// <summary>Factory for creating maintenance setter instances from configuration.</summary>
public interface IMaintenanceSetterFactory
{
    IMaintenanceSetter Create(MaintenanceSetterConfig config);

    bool IsSupported(SetterType type);
}
