namespace ReadyStackGo.Application.UseCases.PrtgConnections;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.PrtgConnections;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

// ─── Queries ─────────────────────────────────────────────────────────

public sealed record ListPrtgConnectionsQuery() : IRequest<IReadOnlyList<PrtgConnectionDto>>;

public sealed record GetPrtgConnectionQuery(Guid Id) : IRequest<PrtgConnectionDto?>;

public sealed class ListPrtgConnectionsHandler : IRequestHandler<ListPrtgConnectionsQuery, IReadOnlyList<PrtgConnectionDto>>
{
    private readonly IPrtgConnectionRepository _repository;
    private readonly IOrganizationRepository _organizations;

    public ListPrtgConnectionsHandler(IPrtgConnectionRepository repository, IOrganizationRepository organizations)
    {
        _repository = repository;
        _organizations = organizations;
    }

    public Task<IReadOnlyList<PrtgConnectionDto>> Handle(ListPrtgConnectionsQuery _, CancellationToken ct)
    {
        var org = _organizations.GetAll().FirstOrDefault();
        if (org is null)
            return Task.FromResult<IReadOnlyList<PrtgConnectionDto>>(Array.Empty<PrtgConnectionDto>());

        var orgId = DeploymentOrganizationId.FromIdentityAccess(org.Id);
        var list = _repository.GetByOrganization(orgId).Select(ToDto).ToList();
        return Task.FromResult<IReadOnlyList<PrtgConnectionDto>>(list);
    }

    internal static PrtgConnectionDto ToDto(PrtgConnection c) => new(
        Id: c.Id.Value.ToString(),
        Name: c.Name,
        Url: c.Url,
        HasApiToken: !string.IsNullOrEmpty(c.EncryptedApiToken),
        TemplateDeviceId: c.TemplateDeviceId,
        VerifyTls: c.VerifyTls,
        CreatedAt: c.CreatedAt,
        UpdatedAt: c.UpdatedAt,
        LastUsedAt: c.LastUsedAt);
}

public sealed class GetPrtgConnectionHandler : IRequestHandler<GetPrtgConnectionQuery, PrtgConnectionDto?>
{
    private readonly IPrtgConnectionRepository _repository;

    public GetPrtgConnectionHandler(IPrtgConnectionRepository repository) => _repository = repository;

    public Task<PrtgConnectionDto?> Handle(GetPrtgConnectionQuery request, CancellationToken ct)
    {
        var conn = _repository.Get(new PrtgConnectionId(request.Id));
        return Task.FromResult(conn is null ? null : ListPrtgConnectionsHandler.ToDto(conn));
    }
}

// ─── Commands ────────────────────────────────────────────────────────

public sealed record CreatePrtgConnectionCommand(CreatePrtgConnectionRequest Request) : IRequest<PrtgConnectionResponse>;

public sealed class CreatePrtgConnectionHandler : IRequestHandler<CreatePrtgConnectionCommand, PrtgConnectionResponse>
{
    private readonly IPrtgConnectionRepository _repository;
    private readonly IOrganizationRepository _organizations;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<CreatePrtgConnectionHandler> _logger;

    public CreatePrtgConnectionHandler(
        IPrtgConnectionRepository repository,
        IOrganizationRepository organizations,
        ICredentialEncryptionService encryption,
        ILogger<CreatePrtgConnectionHandler> logger)
    {
        _repository = repository;
        _organizations = organizations;
        _encryption = encryption;
        _logger = logger;
    }

    public Task<PrtgConnectionResponse> Handle(CreatePrtgConnectionCommand command, CancellationToken ct)
    {
        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.Name))
            return Task.FromResult(new PrtgConnectionResponse(false, "Name is required."));
        if (string.IsNullOrWhiteSpace(req.Url))
            return Task.FromResult(new PrtgConnectionResponse(false, "URL is required."));
        if (string.IsNullOrWhiteSpace(req.ApiToken))
            return Task.FromResult(new PrtgConnectionResponse(false, "API token is required."));

        var org = _organizations.GetAll().FirstOrDefault();
        if (org is null)
            return Task.FromResult(new PrtgConnectionResponse(false, "Organization not set. Complete the setup wizard first."));

        var orgId = DeploymentOrganizationId.FromIdentityAccess(org.Id);

        if (_repository.GetByName(orgId, req.Name) is not null)
            return Task.FromResult(new PrtgConnectionResponse(false, $"A PRTG connection named '{req.Name}' already exists."));

        try
        {
            var encrypted = _encryption.Encrypt(req.ApiToken);
            var conn = PrtgConnection.Create(
                PrtgConnectionId.Create(),
                orgId,
                req.Name,
                req.Url,
                encrypted,
                req.TemplateDeviceId,
                req.VerifyTls);

            _repository.Add(conn);
            _repository.SaveChanges();

            _logger.LogInformation("Created PRTG connection {Id} '{Name}' ({Url})", conn.Id, conn.Name, conn.Url);
            return Task.FromResult(new PrtgConnectionResponse(true, Connection: ListPrtgConnectionsHandler.ToDto(conn)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create PRTG connection '{Name}'", req.Name);
            return Task.FromResult(new PrtgConnectionResponse(false, ex.Message));
        }
    }
}

public sealed record UpdatePrtgConnectionCommand(Guid Id, UpdatePrtgConnectionRequest Request) : IRequest<PrtgConnectionResponse>;

public sealed class UpdatePrtgConnectionHandler : IRequestHandler<UpdatePrtgConnectionCommand, PrtgConnectionResponse>
{
    private readonly IPrtgConnectionRepository _repository;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<UpdatePrtgConnectionHandler> _logger;

    public UpdatePrtgConnectionHandler(
        IPrtgConnectionRepository repository,
        ICredentialEncryptionService encryption,
        ILogger<UpdatePrtgConnectionHandler> logger)
    {
        _repository = repository;
        _encryption = encryption;
        _logger = logger;
    }

    public Task<PrtgConnectionResponse> Handle(UpdatePrtgConnectionCommand command, CancellationToken ct)
    {
        var conn = _repository.Get(new PrtgConnectionId(command.Id));
        if (conn is null)
            return Task.FromResult(new PrtgConnectionResponse(false, "PRTG connection not found."));

        var req = command.Request;

        try
        {
            if (!string.IsNullOrWhiteSpace(req.Name) && req.Name != conn.Name)
                conn.Rename(req.Name);
            if (!string.IsNullOrWhiteSpace(req.Url) && req.Url != conn.Url)
                conn.UpdateUrl(req.Url);
            if (!string.IsNullOrEmpty(req.ApiToken))
                conn.UpdateApiToken(_encryption.Encrypt(req.ApiToken));
            if (req.TemplateDeviceId != conn.TemplateDeviceId)
                conn.UpdateTemplateDeviceId(req.TemplateDeviceId);
            if (req.VerifyTls != conn.VerifyTls)
                conn.UpdateVerifyTls(req.VerifyTls);

            _repository.Update(conn);
            _repository.SaveChanges();

            _logger.LogInformation("Updated PRTG connection {Id} '{Name}'", conn.Id, conn.Name);
            return Task.FromResult(new PrtgConnectionResponse(true, Connection: ListPrtgConnectionsHandler.ToDto(conn)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update PRTG connection {Id}", command.Id);
            return Task.FromResult(new PrtgConnectionResponse(false, ex.Message));
        }
    }
}

public sealed record DeletePrtgConnectionCommand(Guid Id) : IRequest<PrtgConnectionResponse>;

public sealed class DeletePrtgConnectionHandler : IRequestHandler<DeletePrtgConnectionCommand, PrtgConnectionResponse>
{
    private readonly IPrtgConnectionRepository _repository;
    private readonly ILogger<DeletePrtgConnectionHandler> _logger;

    public DeletePrtgConnectionHandler(IPrtgConnectionRepository repository, ILogger<DeletePrtgConnectionHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<PrtgConnectionResponse> Handle(DeletePrtgConnectionCommand command, CancellationToken ct)
    {
        var conn = _repository.Get(new PrtgConnectionId(command.Id));
        if (conn is null)
            return Task.FromResult(new PrtgConnectionResponse(false, "PRTG connection not found."));

        _repository.Delete(conn);
        _repository.SaveChanges();

        _logger.LogInformation("Deleted PRTG connection {Id} '{Name}'", conn.Id, conn.Name);
        return Task.FromResult(new PrtgConnectionResponse(true));
    }
}
