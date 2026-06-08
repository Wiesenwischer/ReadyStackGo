namespace ReadyStackGo.Application.Services;

/// <summary>
/// Thin wrapper around the PRTG HTTP API for the lifecycle handlers behind
/// Variant 3. The PRTG API is a URL/query-string-based "API" — every call is
/// a GET or POST against an .htm endpoint with parameters in the URL.
/// </summary>
public interface IPrtgApiClient
{
    /// <summary>
    /// Duplicates a template device and returns the new device's PRTG object-id.
    /// The new device starts paused; call <see cref="ResumeAsync"/> to start polling.
    /// </summary>
    Task<int?> DuplicateDeviceAsync(
        PrtgConnectionInfo connection,
        int templateDeviceId,
        string newName,
        string host,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a fresh device under <paramref name="groupId"/> using <c>/api/adddevice2.htm</c>.
    /// Returns the new device's object-id. No sensors are attached — call
    /// <see cref="TriggerAutoDiscoveryAsync"/> afterwards so PRTG runs its
    /// auto-discovery against the imported RSGO device template (V1 bundle).
    /// </summary>
    Task<int?> AddDeviceAsync(
        PrtgConnectionInfo connection,
        int groupId,
        string deviceName,
        string host,
        CancellationToken cancellationToken);

    /// <summary>
    /// Triggers auto-discovery on a device (<c>/api/discovernow.htm</c>). PRTG
    /// then walks SNMP / WMI / etc. against the device's host and adds matching
    /// sensors — including any from the imported RSGO MIB device template.
    /// </summary>
    Task<bool> TriggerAutoDiscoveryAsync(
        PrtgConnectionInfo connection,
        int deviceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds an SNMP Custom (numeric scalar) sensor to a PRTG device via
    /// <c>/api/addsensor5.htm</c>. Works without MIB import on the PRTG side
    /// because we pass the numeric OID directly.
    /// </summary>
    Task<bool> AddSnmpCustomSensorAsync(
        PrtgConnectionInfo connection,
        int deviceId,
        string sensorName,
        string numericOid,
        string unit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds an SNMP Custom String sensor to a PRTG device — polls a single
    /// OID that returns a string (e.g. RSGO's <c>rsgoSystemVersion</c>).
    /// </summary>
    Task<bool> AddSnmpCustomStringSensorAsync(
        PrtgConnectionInfo connection,
        int deviceId,
        string sensorName,
        string numericOid,
        CancellationToken cancellationToken);

    /// <summary>Sets a property on a PRTG object (e.g. <c>host</c>, <c>name</c>).</summary>
    Task<bool> SetObjectPropertyAsync(
        PrtgConnectionInfo connection,
        int objectId,
        string propertyName,
        string value,
        CancellationToken cancellationToken);

    /// <summary>Resumes (unpauses) a PRTG object so it starts polling.</summary>
    Task<bool> ResumeAsync(PrtgConnectionInfo connection, int objectId, CancellationToken cancellationToken);

    /// <summary>Deletes a PRTG object. Returns true if deleted or if the object did not exist.</summary>
    Task<bool> DeleteObjectAsync(PrtgConnectionInfo connection, int objectId, CancellationToken cancellationToken);

    /// <summary>Lightweight liveness probe — returns true when the URL responds and the API token authenticates.</summary>
    Task<bool> PingAsync(PrtgConnectionInfo connection, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all groups visible to the given API token. The connection form
    /// asks for a <em>target group</em> — RSGO creates new devices inside it
    /// via <see cref="AddDeviceAsync"/> when deployments are linked.
    /// </summary>
    Task<IReadOnlyList<PrtgGroupSummary>> ListGroupsAsync(
        PrtgConnectionInfo connection, CancellationToken cancellationToken);
}

/// <summary>One row from PRTG's group table — just what the dropdown needs.</summary>
public sealed record PrtgGroupSummary(int ObjectId, string Name, string? Probe);

/// <summary>
/// Per-call connection info. Decrypted by the caller (lifecycle handler) so the
/// HTTP client itself does not depend on the encryption service.
/// </summary>
public sealed record PrtgConnectionInfo(string BaseUrl, string ApiToken, bool VerifyTls);
