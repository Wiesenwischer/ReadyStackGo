using FastEndpoints;
using Microsoft.Extensions.Options;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Snmp;

namespace ReadyStackGo.Api.Endpoints.Snmp;

/// <summary>
/// GET /api/snmp/status — read-only view of the current SNMP agent
/// configuration (Enabled flag, Port, ListenAddress, RootOid, v2c community
/// configured y/n, v3 user count). Writing settings via UI is a follow-up
/// (v0.65); for v0.64 settings are managed in appsettings.json and require
/// a container restart.
/// </summary>
[RequirePermission("Settings", "Read")]
public class GetSnmpStatusEndpoint : EndpointWithoutRequest<SnmpStatusDto>
{
    private readonly SnmpAgentOptions _options;

    public GetSnmpStatusEndpoint(IOptions<SnmpAgentOptions> options)
    {
        _options = options.Value;
    }

    public override void Configure()
    {
        Get("/api/snmp/status");
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Response = new SnmpStatusDto
        {
            Enabled = _options.Enabled,
            Port = _options.Port,
            ListenAddress = _options.ListenAddress,
            RootOid = _options.RootOid,
            V2cConfigured = !string.IsNullOrWhiteSpace(_options.Community),
            V3UserCount = _options.V3Users?.Count ?? 0,
        };
        return Task.CompletedTask;
    }
}

public class SnmpStatusDto
{
    public bool Enabled { get; init; }
    public int Port { get; init; }
    public string ListenAddress { get; init; } = string.Empty;
    public string RootOid { get; init; } = string.Empty;
    public bool V2cConfigured { get; init; }
    public int V3UserCount { get; init; }
}
