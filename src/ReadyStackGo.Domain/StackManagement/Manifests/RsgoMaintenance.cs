namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Maintenance configuration section.
/// Contains observer and future maintenance-related settings.
/// </summary>
public class RsgoMaintenance
{
    /// <summary>
    /// Observer configuration for automatic maintenance mode detection (RSGO reads state).
    /// </summary>
    public RsgoMaintenanceObserver? Observer { get; set; }

    /// <summary>
    /// Setter configuration for propagating RSGO-initiated maintenance transitions to the
    /// product (RSGO writes state). Mirror image of the observer.
    /// </summary>
    public RsgoMaintenanceSetter? Setter { get; set; }
}
