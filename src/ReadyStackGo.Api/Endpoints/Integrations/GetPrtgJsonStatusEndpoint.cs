using System.Text.Json;
using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Integrations.Prtg;

namespace ReadyStackGo.Api.Endpoints.Integrations;

/// <summary>
/// GET /api/integrations/prtg/status — returns RSGO health as a PRTG "HTTP
/// Data Advanced" JSON envelope. An admin pastes this URL (with an API key
/// either as <c>X-Api-Key</c> header or <c>?apikey=</c> query parameter) into
/// a single PRTG sensor. No template install, no probe restart, no MIB import.
///
/// Variant 4 of the PRTG integration; see docs/Plans/PLAN-prtg-http-json-sensor.md.
/// </summary>
[RequirePermission("Settings", "Read")]
public class GetPrtgJsonStatusEndpoint : EndpointWithoutRequest<PrtgJsonStatusResponse>
{
    private readonly IMediator _mediator;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null, // exact PRTG attribute names are case-sensitive
        DefaultIgnoreCondition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public GetPrtgJsonStatusEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure()
    {
        Get("/api/integrations/prtg/status");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPrtgJsonStatusQuery(), ct);

        // PRTG-quirk: the keys "channel", "value", "ValueLookup" etc. are
        // case-sensitive and naming is mixed PascalCase / camelCase /
        // lowercase. Bypass FastEndpoints' default serializer (which would
        // CamelCase everything) and write our own JSON with the explicit
        // [JsonPropertyName] attributes on the model.
        HttpContext.Response.ContentType = "application/json";
        HttpContext.Response.Headers.CacheControl = "max-age=15, private";
        await JsonSerializer.SerializeAsync(HttpContext.Response.Body, response, SerializerOptions, ct);
    }
}
