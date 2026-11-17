namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Maintenance setter configuration — the mirror image of the observer.
/// When RSGO transitions a product into/out of maintenance manually (not via the observer),
/// the setter actively propagates that state to the product:
/// - sqlExtendedProperty: writes the same SQL Server extended property the observer reads.
/// - webhook: POSTs a signed { "state": "maintenance" | "normal" } to a product endpoint.
/// </summary>
public class RsgoMaintenanceSetter
{
    /// <summary>Setter type: "sqlExtendedProperty" or "webhook".</summary>
    public required string Type { get; set; }

    /// <summary>
    /// Value written to indicate maintenance is active (sqlExtendedProperty). Should match the
    /// observer's maintenanceValue so both paths stay consistent.
    /// </summary>
    public string? MaintenanceValue { get; set; }

    /// <summary>Value written to indicate normal operation (sqlExtendedProperty).</summary>
    public string? NormalValue { get; set; }

    /// <summary>
    /// Optional delay between firing the setter and stopping the containers, so the product can
    /// drain its clients (e.g. "10s"). Default 0 (no behavior change).
    /// </summary>
    public string? GracePeriod { get; set; }

    #region SQL setter (sqlExtendedProperty)

    /// <summary>Direct connection string. Supports variable substitution: ${VAR_NAME}.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Name of a manifest variable containing the connection string.</summary>
    public string? ConnectionName { get; set; }

    /// <summary>Name of the SQL Server extended property to write (class = 0, database-level).</summary>
    public string? PropertyName { get; set; }

    #endregion

    #region Webhook setter

    /// <summary>URL the product exposes to receive the maintenance state. Supports ${VAR_NAME}.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// HMAC secret used to sign the request body (SHA-256). Supports ${VAR_NAME}.
    /// Never logged.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>Request timeout (e.g. "10s"). Defaults to 10s.</summary>
    public string? Timeout { get; set; }

    /// <summary>Number of retries on failure. Defaults to 2.</summary>
    public int? Retries { get; set; }

    #endregion
}
