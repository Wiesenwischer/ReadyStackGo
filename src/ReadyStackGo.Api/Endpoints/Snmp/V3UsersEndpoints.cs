using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Snmp.V3Users;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Api.Endpoints.Snmp;

[RequirePermission("Settings", "Read")]
public class ListV3UsersEndpoint : EndpointWithoutRequest<IReadOnlyList<V3UserDto>>
{
    private readonly IMediator _mediator;
    public ListV3UsersEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Get("/api/snmp/v3-users");

    public override async Task HandleAsync(CancellationToken ct)
    {
        Response = await _mediator.Send(new ListV3UsersQuery(), ct);
    }
}

[RequirePermission("Settings", "Manage")]
public class AddV3UserEndpoint : Endpoint<AddV3UserRequest, AddV3UserResponse>
{
    private readonly IMediator _mediator;
    public AddV3UserEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Post("/api/snmp/v3-users");

    public override async Task HandleAsync(AddV3UserRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddV3UserCommand(
            req.Name, req.AuthProtocol, req.AuthPassphrase ?? string.Empty,
            req.PrivProtocol, req.PrivPassphrase ?? string.Empty), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed", StatusCodes.Status400BadRequest);
            return;
        }
        Response = new AddV3UserResponse { Id = result.UserId!.Value };
    }
}

public class AddV3UserRequest
{
    public string Name { get; set; } = string.Empty;
    public SnmpAuthProtocol AuthProtocol { get; set; }
    public string? AuthPassphrase { get; set; }
    public SnmpPrivProtocol PrivProtocol { get; set; }
    public string? PrivPassphrase { get; set; }
}

public class AddV3UserResponse
{
    public Guid Id { get; init; }
}

[RequirePermission("Settings", "Manage")]
public class UpdateV3UserEndpoint : Endpoint<UpdateV3UserRequest>
{
    private readonly IMediator _mediator;
    public UpdateV3UserEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Put("/api/snmp/v3-users/{Id}");

    public override async Task HandleAsync(UpdateV3UserRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateV3UserCommand(
            req.Id, req.AuthProtocol, req.AuthPassphrase,
            req.PrivProtocol, req.PrivPassphrase), ct);
        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed", StatusCodes.Status400BadRequest);
            return;
        }
        await Send.NoContentAsync(ct);
    }
}

public class UpdateV3UserRequest
{
    public Guid Id { get; set; }
    public SnmpAuthProtocol AuthProtocol { get; set; }
    public string? AuthPassphrase { get; set; }
    public SnmpPrivProtocol PrivProtocol { get; set; }
    public string? PrivPassphrase { get; set; }
}

[RequirePermission("Settings", "Manage")]
public class DeleteV3UserEndpoint : Endpoint<DeleteV3UserRequest>
{
    private readonly IMediator _mediator;
    public DeleteV3UserEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Delete("/api/snmp/v3-users/{Id}");

    public override async Task HandleAsync(DeleteV3UserRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteV3UserCommand(req.Id), ct);
        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed", StatusCodes.Status404NotFound);
            return;
        }
        await Send.NoContentAsync(ct);
    }
}

public class DeleteV3UserRequest
{
    public Guid Id { get; set; }
}
