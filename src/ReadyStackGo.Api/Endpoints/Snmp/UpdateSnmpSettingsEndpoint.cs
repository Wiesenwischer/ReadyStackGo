using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Snmp.GetSnmpSettings;
using ReadyStackGo.Application.UseCases.Snmp.UpdateSnmpSettings;

namespace ReadyStackGo.Api.Endpoints.Snmp;

[RequirePermission("Settings", "Manage")]
public class UpdateSnmpSettingsEndpoint : Endpoint<UpdateSnmpSettingsRequest, UpdateSnmpSettingsResponse>
{
    private readonly IMediator _mediator;

    public UpdateSnmpSettingsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/snmp/settings");
    }

    public override async Task HandleAsync(UpdateSnmpSettingsRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSnmpSettingsCommand(
            req.Enabled, req.Port, req.ListenAddress, req.RootOid,
            req.Community ?? string.Empty, req.TrapReceivers ?? string.Empty), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Update failed", StatusCodes.Status400BadRequest);
            return;
        }
        Response = new UpdateSnmpSettingsResponse { Success = true };
    }
}

public class UpdateSnmpSettingsRequest
{
    public bool Enabled { get; set; }
    public int Port { get; set; }
    public string ListenAddress { get; set; } = "0.0.0.0";
    public string RootOid { get; set; } = "1.3.6.1.4.1.99999.1";
    public string? Community { get; set; }
    public string? TrapReceivers { get; set; }
}

public class UpdateSnmpSettingsResponse
{
    public bool Success { get; init; }
}

[RequirePermission("Settings", "Read")]
public class GetSnmpSettingsEndpoint : EndpointWithoutRequest<SnmpSettingsDto>
{
    private readonly IMediator _mediator;
    public GetSnmpSettingsEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Get("/api/snmp/settings");

    public override async Task HandleAsync(CancellationToken ct)
    {
        Response = await _mediator.Send(new GetSnmpSettingsQuery(), ct);
    }
}
