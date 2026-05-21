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
        if (deployment?.PrtgConnectionId is null)
            return; // not linked to a PRTG connection — nothing to do

        var conn = _connections.Get(deployment.PrtgConnectionId);
        if (conn is null)
        {
            _logger.LogWarning("ProductDeployment {Id} references missing PrtgConnection {ConnId}",
                deployment.Id, deployment.PrtgConnectionId);
            return;
        }
        if (conn.TemplateDeviceId is null)
        {
            _logger.LogInformation(
                "PRTG connection '{Name}' has no TemplateDeviceId set — skipping auto-register for {ProductName}",
                conn.Name, evt.ProductName);
            return;
        }

        try
        {
            var token = _encryption.Decrypt(conn.EncryptedApiToken);
            var info = new PrtgConnectionInfo(conn.Url, token, conn.VerifyTls);

            // We use the product name; in a future iteration we may include the
            // RSGO host for clarity in multi-RSGO setups.
            var deviceName = $"RSGO: {deployment.ProductName} ({deployment.ProductVersion})";
            var host = ExtractHostFromUrl(conn.Url) ?? "rsgo.local"; // PRTG won't accept empty host

            var newId = await _client.DuplicateDeviceAsync(info, conn.TemplateDeviceId.Value, deviceName, host, ct);
            if (newId is null)
            {
                _logger.LogWarning("PRTG duplicate-device failed for ProductDeployment {Id}", deployment.Id);
                return;
            }

            await _client.ResumeAsync(info, newId.Value, ct);
            deployment.RecordPrtgSync(newId.Value);
            _deployments.Update(deployment);
            _deployments.SaveChanges();

            conn.RecordUsage();
            _connections.Update(conn);
            _connections.SaveChanges();

            _logger.LogInformation("Registered ProductDeployment {Id} as PRTG device {PrtgId} on '{ConnName}'",
                deployment.Id, newId, conn.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PRTG auto-register failed for ProductDeployment {Id}", deployment.Id);
        }
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
        if (deployment?.PrtgConnectionId is null || deployment.PrtgDeviceId is null)
            return; // never registered, or already cleaned up

        var conn = connections.Get(deployment.PrtgConnectionId);
        if (conn is null) return;

        try
        {
            var token = encryption.Decrypt(conn.EncryptedApiToken);
            var info = new PrtgConnectionInfo(conn.Url, token, conn.VerifyTls);

            var deleted = await client.DeleteObjectAsync(info, deployment.PrtgDeviceId.Value, ct);
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
