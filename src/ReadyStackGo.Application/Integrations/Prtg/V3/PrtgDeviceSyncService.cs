namespace ReadyStackGo.Application.Integrations.Prtg.V3;

using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.Deployment.PrtgConnections;

public sealed class PrtgDeviceSyncService : IPrtgDeviceSyncService
{
    private readonly IPrtgConnectionRepository _connections;
    private readonly IProductDeploymentRepository _deployments;
    private readonly IPrtgApiClient _client;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<PrtgDeviceSyncService> _logger;

    public PrtgDeviceSyncService(
        IPrtgConnectionRepository connections,
        IProductDeploymentRepository deployments,
        IPrtgApiClient client,
        ICredentialEncryptionService encryption,
        ILogger<PrtgDeviceSyncService> logger)
    {
        _connections = connections;
        _deployments = deployments;
        _client = client;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<PrtgSyncResult> RegisterAsync(ProductDeployment deployment, CancellationToken ct)
    {
        if (!deployment.HasPrtgTarget)
            return PrtgSyncResult.Skipped("Deployment has no PRTG target configured.");

        var target = ResolveTarget(deployment);
        if (target is null)
            return PrtgSyncResult.Skipped(
                "PRTG target is incomplete (no Target Group selected — RSGO needs a PRTG group under which to create devices).");

        // Legacy lifecycle-handler path — no explicit RSGO host; falls back
        // to the PRTG URL host (best-effort, admins can edit afterwards).
        return await CreateDeviceAsync(deployment, target.Value.Info, target.Value.TargetGroupId,
            explicitRsgoHost: null, target.Value.Connection, ct);
    }

    public async Task<PrtgSyncResult> RegisterInGroupAsync(
        ProductDeployment deployment, int targetGroupId, string? rsgoHost, CancellationToken ct)
    {
        if (deployment.PrtgConnectionId is null)
            return PrtgSyncResult.Skipped("Deployment is not linked to a PRTG connection.");

        var conn = _connections.Get(deployment.PrtgConnectionId);
        if (conn is null)
            return PrtgSyncResult.Failed("Linked PRTG connection was not found.");

        var token = _encryption.Decrypt(conn.EncryptedApiToken);
        var info = new PrtgConnectionInfo(conn.Url, token, conn.VerifyTls);
        return await CreateDeviceAsync(deployment, info, targetGroupId, rsgoHost, conn, ct);
    }

    private async Task<PrtgSyncResult> CreateDeviceAsync(
        ProductDeployment deployment, PrtgConnectionInfo info, int targetGroupId,
        string? explicitRsgoHost, PrtgConnection? connectionToRecordUsage, CancellationToken ct)
    {
        try
        {
            var deviceName = $"RSGO: {deployment.ProductName} ({deployment.ProductVersion})";
            // PRTG-Device host MUST be the RSGO instance (the SNMP agent runs
            // there). When called from the user-driven flow we pass the host
            // from the incoming HTTP request — the address the admin used to
            // reach RSGO, which is almost always reachable from PRTG too. The
            // legacy lifecycle-handler path falls back to the PRTG URL host
            // (best-effort; admins can re-target the device manually in PRTG).
            var host = explicitRsgoHost
                ?? ExtractHostFromUrl(info.BaseUrl)
                ?? "rsgo.local";

            var newId = await _client.AddDeviceAsync(info, targetGroupId, deviceName, host, ct);
            if (newId is null)
            {
                _logger.LogWarning("PRTG adddevice2 failed for ProductDeployment {Id}", deployment.Id);
                return PrtgSyncResult.Failed("PRTG rejected the add-device call. Check the target group id and API token.");
            }

            // Add RSGO-specific SNMP sensors via the PRTG API. We feed the
            // numeric OIDs directly so PRTG does NOT need the RSGO MIB to be
            // imported — the sensors poll the OIDs and display the values.
            // (PRTG MIB browser would only resolve the symbolic names if the
            // MIB is imported; the polled values work either way.)
            var sensorResults = await AddSystemSensorsAsync(info, newId.Value, ct);
            _logger.LogInformation(
                "Added {Ok}/{Total} RSGO SNMP sensors to PRTG device {DeviceId} (deployment {Id})",
                sensorResults.OkCount, sensorResults.Total, newId, deployment.Id);

            deployment.RecordPrtgSync(newId.Value);
            _deployments.Update(deployment);
            _deployments.SaveChanges();

            if (connectionToRecordUsage is not null)
            {
                connectionToRecordUsage.RecordUsage();
                _connections.Update(connectionToRecordUsage);
                _connections.SaveChanges();
            }

            _logger.LogInformation("Registered ProductDeployment {Id} as PRTG device {PrtgId} on {Url}",
                deployment.Id, newId, info.BaseUrl);
            return PrtgSyncResult.Ok(newId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PRTG auto-register failed for ProductDeployment {Id}", deployment.Id);
            return PrtgSyncResult.Failed($"PRTG call failed: {ex.Message}");
        }
    }

    public async Task<PrtgSyncResult> DeregisterAsync(ProductDeploymentId deploymentId, CancellationToken ct)
    {
        var deployment = _deployments.Get(deploymentId);
        if (deployment is null || deployment.PrtgDeviceId is null)
            return PrtgSyncResult.Skipped("No PRTG device registered for this deployment.");

        var target = ResolveTarget(deployment);
        if (target is null)
        {
            // The link was already cleared but we still have a PrtgDeviceId — we
            // can't talk to PRTG without a target. Clear local state so the
            // record matches reality.
            _logger.LogWarning("ProductDeployment {Id} has a PrtgDeviceId but no resolvable target — clearing local state",
                deployment.Id);
            return PrtgSyncResult.Skipped("Target gone; local PrtgDeviceId still set.");
        }

        try
        {
            var deleted = await _client.DeleteObjectAsync(target.Value.Info, deployment.PrtgDeviceId.Value, ct);
            if (deleted)
            {
                _logger.LogInformation("Deleted PRTG device {PrtgId} for ProductDeployment {Id}",
                    deployment.PrtgDeviceId, deployment.Id);
                return PrtgSyncResult.Ok();
            }

            _logger.LogWarning("PRTG delete-object failed for device {PrtgId} (ProductDeployment {Id})",
                deployment.PrtgDeviceId, deployment.Id);
            return PrtgSyncResult.Failed("PRTG rejected the delete-object call.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PRTG deregister failed for ProductDeployment {Id}", deployment.Id);
            return PrtgSyncResult.Failed($"PRTG call failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves a deployment's PRTG target — either a saved connection (V3, wins
    /// when both are set) or the inline per-deployment credentials (V2). Returns
    /// null when the target is misconfigured (e.g. missing group id).
    ///
    /// Note: the column is still called <c>TemplateDeviceId</c> in the database
    /// for historical reasons; semantically it is now the PRTG <em>group</em>
    /// id under which RSGO creates new devices via <c>adddevice2</c>.
    /// </summary>
    private (PrtgConnectionInfo Info, int TargetGroupId, PrtgConnection? Connection)? ResolveTarget(
        ProductDeployment deployment)
    {
        if (deployment.PrtgConnectionId is not null)
        {
            var conn = _connections.Get(deployment.PrtgConnectionId);
            if (conn is null)
            {
                _logger.LogWarning("ProductDeployment {Id} references missing PrtgConnection {ConnId}",
                    deployment.Id, deployment.PrtgConnectionId);
                return null;
            }
            if (conn.TemplateDeviceId is null)
            {
                _logger.LogInformation(
                    "PRTG connection '{Name}' has no Target Group set — skipping auto-register",
                    conn.Name);
                return null;
            }
            var token = _encryption.Decrypt(conn.EncryptedApiToken);
            return (new PrtgConnectionInfo(conn.Url, token, conn.VerifyTls), conn.TemplateDeviceId.Value, conn);
        }

        if (!string.IsNullOrEmpty(deployment.InlinePrtgUrl)
            && !string.IsNullOrEmpty(deployment.InlinePrtgEncryptedToken))
        {
            if (deployment.InlinePrtgTemplateDeviceId is null)
            {
                _logger.LogInformation(
                    "ProductDeployment {Id} has inline PRTG credentials but no TemplateDeviceId — skipping auto-register",
                    deployment.Id);
                return null;
            }
            var token = _encryption.Decrypt(deployment.InlinePrtgEncryptedToken);
            return (new PrtgConnectionInfo(deployment.InlinePrtgUrl, token, deployment.InlinePrtgVerifyTls),
                    deployment.InlinePrtgTemplateDeviceId.Value, null);
        }

        return null;
    }

    private static string? ExtractHostFromUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;

    /// <summary>
    /// Adds the RSGO system-scalar SNMP sensors to a freshly created PRTG
    /// device. These are the "is RSGO alive and healthy" sensors — the user
    /// sees them populated immediately without having to import the MIB or
    /// run auto-discovery. OIDs match <see cref="OidTreeBuilder"/>'s system
    /// scalar layout (.1.x.0 under the RootOid).
    /// </summary>
    private async Task<(int OkCount, int Total)> AddSystemSensorsAsync(
        PrtgConnectionInfo info, int deviceId, CancellationToken ct)
    {
        // Hard-coded to the default IANA-assigned root. A future refactor
        // could read SnmpAgentOptions.RootOid here, but switching from the
        // default is rare and the sensors can be added manually if so.
        const string root = "1.3.6.1.4.1.65846.1";

        var numericSensors = new (string Name, string Oid, string Unit)[]
        {
            ($"RSGO Uptime",          $"{root}.1.2.0", "Seconds"),
            ($"RSGO Environments",    $"{root}.1.3.0", "Count"),
            ($"RSGO Sources",         $"{root}.1.4.0", "Count"),
            ($"RSGO Database Health", $"{root}.1.5.0", "Custom"),
        };

        var ok = 0;
        var total = numericSensors.Length + 1; // + version (string)

        foreach (var s in numericSensors)
        {
            if (await _client.AddSnmpCustomSensorAsync(info, deviceId, s.Name, s.Oid, s.Unit, ct))
                ok++;
        }

        // Version string sensor.
        if (await _client.AddSnmpCustomStringSensorAsync(info, deviceId, "RSGO Version", $"{root}.1.1.0", ct))
            ok++;

        return (ok, total);
    }
}
