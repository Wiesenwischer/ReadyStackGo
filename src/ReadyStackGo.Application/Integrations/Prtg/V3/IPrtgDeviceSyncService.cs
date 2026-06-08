namespace ReadyStackGo.Application.Integrations.Prtg.V3;

using ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Pushes a ProductDeployment's PRTG-target state into PRTG: creates the device
/// when a link is established, deletes it when the link is removed. Reused by
/// (a) the lifecycle event handlers — fire on Completed / Removed / Superseded —
/// and (b) the LinkPrtgConnectionHandler — fires immediately when an admin
/// links or unlinks a connection in the UI, so the device appears / disappears
/// in PRTG without having to wait for a redeploy.
/// </summary>
public interface IPrtgDeviceSyncService
{
    /// <summary>
    /// Creates the PRTG device for a deployment that has a configured PRTG
    /// target (saved connection or inline). Used by lifecycle handlers — the
    /// target group must already be known (either stored on the deployment
    /// itself in a future iteration, or resolved from the inline credentials
    /// in the V2 path). For the explicit user-driven "Add to PRTG" flow use
    /// <see cref="RegisterInGroupAsync"/> instead, which takes the group id
    /// directly.
    /// </summary>
    Task<PrtgSyncResult> RegisterAsync(ProductDeployment deployment, CancellationToken ct);

    /// <summary>
    /// Creates a fresh PRTG device under <paramref name="targetGroupId"/> for
    /// this deployment, sets its <paramref name="rsgoHost"/> as the SNMP host
    /// (so PRTG polls RSGO and not itself), and adds the RSGO SNMP Custom
    /// Table sensors directly via the PRTG API — no MIB import required.
    /// Used by the "Add to PRTG monitoring" dialog.
    /// </summary>
    Task<PrtgSyncResult> RegisterInGroupAsync(
        ProductDeployment deployment, int targetGroupId, string? rsgoHost, CancellationToken ct);

    /// <summary>
    /// Deletes the PRTG device previously registered for this deployment, if
    /// any. No-op when <c>PrtgDeviceId</c> is null. Best-effort like Register.
    /// </summary>
    Task<PrtgSyncResult> DeregisterAsync(ProductDeploymentId deploymentId, CancellationToken ct);
}

/// <summary>
/// Outcome of an attempt to push deployment state to PRTG. <c>Success=true</c>
/// + <c>PrtgDeviceId=null</c> means "nothing to do" (no target configured or
/// nothing to delete). <c>Success=false</c> means PRTG rejected the call or
/// the target is misconfigured; <c>Error</c> contains the user-readable reason.
/// </summary>
public sealed record PrtgSyncResult(bool Success, int? PrtgDeviceId, string? Error)
{
    public static PrtgSyncResult Ok(int? deviceId = null) => new(true, deviceId, null);
    public static PrtgSyncResult Skipped(string reason) => new(true, null, reason);
    public static PrtgSyncResult Failed(string reason) => new(false, null, reason);
}
