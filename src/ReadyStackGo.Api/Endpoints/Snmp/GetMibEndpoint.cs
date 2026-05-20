using System.Reflection;
using FastEndpoints;
using ReadyStackGo.Api.Authorization;

namespace ReadyStackGo.Api.Endpoints.Snmp;

/// <summary>
/// GET /api/snmp/mib — serves the READYSTACKGO-MIB.txt file as plain text so
/// monitoring tools can import it into their MIB browser.
/// </summary>
[RequirePermission("Settings", "Read")]
public class GetMibEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/snmp/mib");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var assembly = typeof(GetMibEndpoint).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Resources.READYSTACKGO-MIB.txt";

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            ThrowError("MIB resource not found in assembly", StatusCodes.Status500InternalServerError);
            return;
        }

        HttpContext.Response.ContentType = "text/plain";
        HttpContext.Response.Headers.ContentDisposition = "attachment; filename=\"READYSTACKGO-MIB.txt\"";
        await stream.CopyToAsync(HttpContext.Response.Body, ct);
    }
}
