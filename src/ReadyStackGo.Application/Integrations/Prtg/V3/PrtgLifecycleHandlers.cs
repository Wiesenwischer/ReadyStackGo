using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.Deployment.PrtgConnections;

namespace ReadyStackGo.Application.Integrations.Prtg.V3;

/// <summary>
/// MediatR notification handlers for Variant 3 (PrtgConnection-based auto-register).
/// Reacts to the ProductDeployment lifecycle and tells the linked PRTG instance
/// to create / delete a corresponding device.
///
/// All handlers are best-effort: a PRTG failure must not break the RSGO
/// deployment flow. Errors are logged and the deployment continues.
/// </summary>
public sealed class PrtgRegisterDeviceOnCompletedHandler
    : INotificationHandler<DomainEventNotification<ProductDeploymentCompleted>>
{
    private readonly IPrtgConnectionRepository _connections;
    private readonly IProductDeploymentRepository _deployments;
    private readonly IPrtgApiClient _client;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<PrtgRegisterDeviceOnCompletedHandler> _logger;

    public PrtgRegisterDeviceOnCompletedHandler(
        IPrtgConnectionRepository connections,
        IProductDeploymentRepository deployments,
        IPrtgApiClient client,
        ICredentialEncryptionService encryption,
        ILogger<PrtgRegisterDeviceOnCompletedHandler> logger)
    {
        _connections = connections;
        _deployments = deployments;
        _client = client;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<ProductDeploymentCompleted> notification, CancellationToken ct)
    {
        var evt = notification.DomainEvent;
        var deployment = _deployments.Get(evt.ProductDeploymentId);
        if (deployment is null || !deployment.HasPrtgTarget)
            return; // no PRTG target configured — nothing to do

        // Resolve target: saved connection (V3) wins over inline (V2).
        var target = ResolvePrtgTarget(deployment, _connections, _encryption, _logger);
        if (target is null)
            return; // logged in ResolvePrtgTarget

        try
        {
            // We use the product name; in a future iteration we may include the
            // RSGO host for clarity in multi-RSGO setups.
            var deviceName = $"RSGO: {deployment.ProductName} ({deployment.ProductVersion})";
            var host = ExtractHostFromUrl(target.Value.Info.BaseUrl) ?? "rsgo.local"; // PRTG won't accept empty host

            var newId = await _client.DuplicateDeviceAsync(target.Value.Info, target.Value.TemplateDeviceId, deviceName, host, ct);
            if (newId is null)
            {
                _logger.LogWarning("PRTG duplicate-device failed for ProductDeployment {Id}", deployment.Id);
                return;
            }

            await _client.ResumeAsync(target.Value.Info, newId.Value, ct);
            deployment.RecordPrtgSync(newId.Value);
            _deployments.Update(deployment);
            _deployments.SaveChanges();

            if (target.Value.Connection is not null)
            {
                target.Value.Connection.RecordUsage();
                _connections.Update(target.Value.Connection);
                _connections.SaveChanges();
            }

            _logger.LogInformation("Registered ProductDeployment {Id} as PRTG device {PrtgId} on {Url}",
                deployment.Id, newId, target.Value.Info.BaseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PRTG auto-register failed for ProductDeployment {Id}", deployment.Id);
        }
    }

    /// <summary>
    /// Resolves a deployment's PRTG target — either a saved connection (V3, wins
    /// when both are set) or the inline per-deployment credentials (V2). Logs
    /// and returns null when the target is misconfigured (e.g. missing template).
    /// </summary>
    internal static (PrtgConnectionInfo Info, int TemplateDeviceId, PrtgConnection? Connection)?
        ResolvePrtgTarget(
            ProductDeployment deployment,
            IPrtgConnectionRepository connections,
            ICredentialEncryptionService encryption,
            ILogger logger)
    {
        if (deployment.PrtgConnectionId is not null)
        {
            var conn = connections.Get(deployment.PrtgConnectionId);
            if (conn is null)
            {
                logger.LogWarning("ProductDeployment {Id} references missing PrtgConnection {ConnId}",
                    deployment.Id, deployment.PrtgConnectionId);
                return null;
            }
            if (conn.TemplateDeviceId is null)
            {
                logger.LogInformation(
                    "PRTG connection '{Name}' has no TemplateDeviceId set — skipping auto-register",
                    conn.Name);
                return null;
            }
            var token = encryption.Decrypt(conn.EncryptedApiToken);
            return (new PrtgConnectionInfo(conn.Url, token, conn.VerifyTls), conn.TemplateDeviceId.Value, conn);
        }

        // Inline (V2): URL + encrypted token + optional template id
        if (!string.IsNullOrEmpty(deployment.InlinePrtgUrl)
            && !string.IsNullOrEmpty(deployment.InlinePrtgEncryptedToken))
        {
            if (deployment.InlinePrtgTemplateDeviceId is null)
            {
                logger.LogInformation(
                    "ProductDeployment {Id} has inline PRTG credentials but no TemplateDeviceId — skipping auto-register",
                    deployment.Id);
                return null;
            }
            var token = encryption.Decrypt(deployment.InlinePrtgEncryptedToken);
            return (new PrtgConnectionInfo(deployment.InlinePrtgUrl, token, deployment.InlinePrtgVerifyTls),
                    deployment.InlinePrtgTemplateDeviceId.Value, null);
        }

        return null;
    }

    private static string? ExtractHostFromUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
    }
}

/// <summary>
/// Deregister on ProductDeploymentRemoved: delete the PRTG device.
/// </summary>
public sealed class PrtgDeregisterOnRemovedHandler
    : INotificationHandler<DomainEventNotification<ProductDeploymentRemoved>>
{
    private readonly IPrtgConnectionRepository _connections;
    private readonly IProductDeploymentRepository _deployments;
    private readonly IPrtgApiClient _client;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<PrtgDeregisterOnRemovedHandler> _logger;

    public PrtgDeregisterOnRemovedHandler(
        IPrtgConnectionRepository connections,
        IProductDeploymentRepository deployments,
        IPrtgApiClient client,
        ICredentialEncryptionService encryption,
        ILogger<PrtgDeregisterOnRemovedHandler> logger)
    {
        _connections = connections;
        _deployments = deployments;
        _client = client;
        _encryption = encryption;
        _logger = logger;
    }

    public Task Handle(DomainEventNotification<ProductDeploymentRemoved> notification, CancellationToken ct)
        => Deregister(notification.DomainEvent.ProductDeploymentId, _connections, _deployments, _client, _encryption, _logger, ct);

    internal static async Task Deregister(
        ProductDeploymentId deploymentId,
        IPrtgConnectionRepository connections,
        IProductDeploymentRepository deployments,
        IPrtgApiClient client,
        ICredentialEncryptionService encryption,
        ILogger logger,
        CancellationToken ct)
    {
        var deployment = deployments.Get(deploymentId);
        if (deployment is null || deployment.PrtgDeviceId is null)
            return; // never registered, or already cleaned up

        // Resolve the same target the Register handler used. If both saved + inline
        // were cleared since registration, we can't deregister — log and exit.
        var target = PrtgRegisterDeviceOnCompletedHandler
            .ResolvePrtgTarget(deployment, connections, encryption, logger);
        if (target is null)
        {
            logger.LogWarning("ProductDeployment {Id} has a PrtgDeviceId but no resolvable target — skipping deregister",
                deployment.Id);
            return;
        }

        try
        {
            var deleted = await client.DeleteObjectAsync(target.Value.Info, deployment.PrtgDeviceId.Value, ct);
            if (deleted)
            {
                logger.LogInformation("Deleted PRTG device {PrtgId} for ProductDeployment {Id}",
                    deployment.PrtgDeviceId, deployment.Id);
            }
            else
            {
                logger.LogWarning("PRTG delete-object failed for device {PrtgId} (ProductDeployment {Id})",
                    deployment.PrtgDeviceId, deployment.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PRTG deregister failed for ProductDeployment {Id}", deployment.Id);
        }
    }
}

/// <summary>
/// Deregister on ProductDeploymentSuperseded — same as Removed; the successor
/// (the new ProductDeployment after an upgrade) gets its own PRTG device on
/// its own Completed event.
/// </summary>
public sealed class PrtgDeregisterOnSupersededHandler
    : INotificationHandler<DomainEventNotification<ProductDeploymentSuperseded>>
{
    private readonly IPrtgConnectionRepository _connections;
    private readonly IProductDeploymentRepository _deployments;
    private readonly IPrtgApiClient _client;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<PrtgDeregisterOnSupersededHandler> _logger;

    public PrtgDeregisterOnSupersededHandler(
        IPrtgConnectionRepository connections,
        IProductDeploymentRepository deployments,
        IPrtgApiClient client,
        ICredentialEncryptionService encryption,
        ILogger<PrtgDeregisterOnSupersededHandler> logger)
    {
        _connections = connections;
        _deployments = deployments;
        _client = client;
        _encryption = encryption;
        _logger = logger;
    }

    public Task Handle(DomainEventNotification<ProductDeploymentSuperseded> notification, CancellationToken ct)
        => PrtgDeregisterOnRemovedHandler.Deregister(
            notification.DomainEvent.ProductDeploymentId, _connections, _deployments, _client, _encryption, _logger, ct);
}
