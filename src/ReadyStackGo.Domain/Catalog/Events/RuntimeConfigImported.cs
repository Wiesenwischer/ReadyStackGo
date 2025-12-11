namespace ReadyStackGo.Domain.Catalog.Events;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Event raised when runtime configuration is imported from a manifest.
/// Used to notify the Deployment context about maintenance observers, health checks, etc.
/// </summary>
public class RuntimeConfigImported : DomainEvent
{
    /// <summary>
    /// Unique stack identifier (format: sourceId:stackName).
    /// </summary>
    public string StackId { get; }

    /// <summary>
    /// ID of the source this configuration came from.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Maintenance observer configuration if defined.
    /// </summary>
    public ImportedMaintenanceObserver? MaintenanceObserver { get; }

    /// <summary>
    /// Health check configurations for services.
    /// </summary>
    public IReadOnlyList<ImportedHealthCheck> HealthChecks { get; }

    public RuntimeConfigImported(
        string stackId,
        string sourceId,
        ImportedMaintenanceObserver? maintenanceObserver,
        IReadOnlyList<ImportedHealthCheck>? healthChecks = null)
    {
        StackId = stackId;
        SourceId = sourceId;
        MaintenanceObserver = maintenanceObserver;
        HealthChecks = healthChecks ?? Array.Empty<ImportedHealthCheck>();
    }
}

/// <summary>
/// Maintenance observer configuration included in import events.
/// </summary>
public record ImportedMaintenanceObserver(
    string Type,
    string PollingInterval,
    string MaintenanceValue,
    string? NormalValue,
    // SQL Observer
    string? ConnectionString,
    string? ConnectionName,
    string? PropertyName,
    string? Query,
    // HTTP Observer
    string? Url,
    string? Method,
    string? Timeout,
    string? JsonPath,
    // File Observer
    string? Path,
    string? Mode,
    string? ContentPattern);

/// <summary>
/// Health check configuration included in import events.
/// </summary>
public record ImportedHealthCheck(
    string ServiceName,
    string Type,
    string? Path,
    int? Port,
    IReadOnlyList<int>? ExpectedStatusCodes,
    bool Https,
    string? Interval,
    string? Timeout,
    int? Retries);
