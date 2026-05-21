namespace ReadyStackGo.Application.UseCases.PrtgConnections;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.Deployment.PrtgConnections;

public sealed record LinkPrtgConnectionCommand(Guid ProductDeploymentId, Guid? PrtgConnectionId)
    : IRequest<PrtgConnectionResponse>;

public sealed class LinkPrtgConnectionHandler : IRequestHandler<LinkPrtgConnectionCommand, PrtgConnectionResponse>
{
    private readonly IProductDeploymentRepository _deployments;
    private readonly IPrtgConnectionRepository _connections;
    private readonly ILogger<LinkPrtgConnectionHandler> _logger;

    public LinkPrtgConnectionHandler(
        IProductDeploymentRepository deployments,
        IPrtgConnectionRepository connections,
        ILogger<LinkPrtgConnectionHandler> logger)
    {
        _deployments = deployments;
        _connections = connections;
        _logger = logger;
    }

    public Task<PrtgConnectionResponse> Handle(LinkPrtgConnectionCommand command, CancellationToken ct)
    {
        var deployment = _deployments.Get(ProductDeploymentId.FromGuid(command.ProductDeploymentId));
        if (deployment is null)
            return Task.FromResult(new PrtgConnectionResponse(false, "ProductDeployment not found."));

        if (command.PrtgConnectionId is null)
        {
            deployment.UnlinkPrtgConnection();
            _deployments.Update(deployment);
            _deployments.SaveChanges();
            _logger.LogInformation("Unlinked PRTG connection from ProductDeployment {Id}", deployment.Id);
            return Task.FromResult(new PrtgConnectionResponse(true));
        }

        var conn = _connections.Get(new PrtgConnectionId(command.PrtgConnectionId.Value));
        if (conn is null)
            return Task.FromResult(new PrtgConnectionResponse(false, "PRTG connection not found."));

        deployment.LinkPrtgConnection(conn.Id);
        _deployments.Update(deployment);
        _deployments.SaveChanges();

        _logger.LogInformation("Linked ProductDeployment {Id} to PRTG connection '{Name}' ({ConnId})",
            deployment.Id, conn.Name, conn.Id);
        return Task.FromResult(new PrtgConnectionResponse(true));
    }
}
