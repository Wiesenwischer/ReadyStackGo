namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Maintenance configuration section.
/// Contains observer and future maintenance-related settings.
/// </summary>
public class RsgoMaintenance
{
    /// <summary>
    /// Observer configuration for automatic maintenance mode detection.
    /// </summary>
    public RsgoMaintenanceObserver? Observer { get; set; }
}
