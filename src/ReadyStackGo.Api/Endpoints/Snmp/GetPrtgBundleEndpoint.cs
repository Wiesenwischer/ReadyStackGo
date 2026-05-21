using System.Reflection;
using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Snmp.Prtg;

namespace ReadyStackGo.Api.Endpoints.Snmp;

/// <summary>
/// GET /api/snmp/prtg-bundle — produces a ZIP archive containing a PRTG
/// device template, the ReadyStackGo MIB, and value-lookup files. An admin
/// downloads this once and unpacks it into the PRTG install directory; from
/// there PRTG's normal Auto-Discovery handles the rest. RSGO never talks
/// to PRTG — there are no credentials to manage.
/// </summary>
[RequirePermission("Settings", "Read")]
public class GetPrtgBundleEndpoint : EndpointWithoutRequest
{
    private readonly IMediator _mediator;

    public GetPrtgBundleEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure()
    {
        Get("/api/snmp/prtg-bundle");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Re-use the existing MIB resource so PRTG and snmpwalk users see the
        // same MIB content — no risk of drift between two embedded copies.
        var apiAssembly = typeof(GetMibEndpoint).Assembly;
        var mibResource = $"{apiAssembly.GetName().Name}.Resources.READYSTACKGO-MIB.txt";
        await using var mibStream = apiAssembly.GetManifestResourceStream(mibResource);
        if (mibStream is null)
        {
            ThrowError("MIB resource not found in assembly", StatusCodes.Status500InternalServerError);
            return;
        }

        using var ms = new MemoryStream();
        await mibStream.CopyToAsync(ms, ct);
        var mibBytes = ms.ToArray();

        var version = apiAssembly.GetName().Version?.ToString(3) ?? "unknown";
        var host = HttpContext.Request.Host.HasValue ? HttpContext.Request.Host.Value : null;

        var result = await _mediator.Send(
            new GetPrtgBundleQuery(MibBytes: mibBytes, SourceHost: host, RsgoVersion: version),
            ct);

        HttpContext.Response.ContentType = result.ContentType;
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{result.FileName}\"";
        await HttpContext.Response.Body.WriteAsync(result.ZipBytes.AsMemory(), ct);
    }
}
