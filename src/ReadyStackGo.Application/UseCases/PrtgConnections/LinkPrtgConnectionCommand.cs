namespace ReadyStackGo.Application.UseCases.PrtgConnections;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Integrations.Prtg.V3;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.Deployment.PrtgConnections;

/// <summary>
/// Links (or unlinks if <paramref name="PrtgConnectionId"/> is null) a
/// ProductDeployment to a PRTG connection and immediately creates the
/// corresponding PRTG device under <paramref name="TargetGroupId"/>. The
/// admin picks both connection and group at the moment of linking — the
/// connection itself only stores credentials and stays reusable across
/// deployments that may live in different PRTG groups.
/// </summary>
public sealed record LinkPrtgConnectionCommand(
    Guid ProductDeploymentId,
    Guid? PrtgConnectionId,
    int? TargetGroupId,
    string? RsgoHost) : IRequest<PrtgConnectionResponse>;

public sealed class LinkPrtgConnectionHandler : IRequestHandler<LinkPrtgConnectionCommand, PrtgConnectionResponse>
{
    private readonly IProductDeploymentRepository _deployments;
    private readonly IPrtgConnectionRepository _connections;
    private readonly IPrtgDeviceSyncService _sync;
    private readonly ILogger<LinkPrtgConnectionHandler> _logger;

    public LinkPrtgConnectionHandler(
        IProductDeploymentRepository deployments,
        IPrtgConnectionRepository connections,
        IPrtgDeviceSyncService sync,
        ILogger<LinkPrtgConnectionHandler> logger)
    {
        _deployments = deployments;
        _connections = connections;
        _sync = sync;
        _logger = logger;
    }

    public async Task<PrtgConnectionResponse> Handle(LinkPrtgConnectionCommand command, CancellationToken ct)
    {
        var deployment = _deployments.Get(ProductDeploymentId.FromGuid(command.ProductDeploymentId));
        if (deployment is null)
            return new PrtgConnectionResponse(false, "ProductDeployment not found.");

        // Unlink path: delete the PRTG device first (we still know the
        // connection), then clear the link locally.
        if (command.PrtgConnectionId is null)
        {
            var deregister = await _sync.DeregisterAsync(deployment.Id, ct);
            deployment.UnlinkPrtgConnection();
            _deployments.Update(deployment);
            _deployments.SaveChanges();
            _logger.LogInformation("Unlinked PRTG connection from ProductDeployment {Id} (deregister: {Status})",
                deployment.Id, deregister.Error ?? "ok");
            return new PrtgConnectionResponse(true);
        }

        if (command.TargetGroupId is null)
            return new PrtgConnectionResponse(false, "Target group is required when linking to a PRTG connection.");

        var conn = _connections.Get(new PrtgConnectionId(command.PrtgConnectionId.Value));
        if (conn is null)
            return new PrtgConnectionResponse(false, "PRTG connection not found.");

        deployment.LinkPrtgConnection(conn.Id);
        _deployments.Update(deployment);
        _deployments.SaveChanges();

        var registerTarget = _deployments.Get(deployment.Id)!;
        var register = await _sync.RegisterInGroupAsync(
            registerTarget, command.TargetGroupId.Value, command.RsgoHost, ct);

        _logger.LogInformation("Linked ProductDeployment {Id} to PRTG '{Name}' ({ConnId}) group {GroupId} — register: {Status}",
            deployment.Id, conn.Name, conn.Id, command.TargetGroupId, register.Success ? $"device {register.PrtgDeviceId}" : register.Error);

        if (!register.Success)
            return new PrtgConnectionResponse(true,
                $"Linked, but PRTG device creation failed: {register.Error}");

        return new PrtgConnectionResponse(true);
    }
}
