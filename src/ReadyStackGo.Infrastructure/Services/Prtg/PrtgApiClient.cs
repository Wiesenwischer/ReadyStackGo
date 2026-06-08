using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services.Prtg;

/// <summary>
/// PRTG HTTP API implementation. Every PRTG API endpoint is a single GET/POST
/// against an <c>.htm</c> URL with parameters in the query string. There is no
/// JSON request body — PRTG predates "modern" REST. Some endpoints return
/// JSON (the <c>.json</c> variants), most return HTML or a short status string.
/// </summary>
public sealed class PrtgApiClient : IPrtgApiClient
{
    private static readonly Regex IdRegex = new(@"""objid""\s*:\s*(\d+)|<a\s+href=[""']/device\.htm\?id=(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PrtgApiClient> _logger;

    public PrtgApiClient(IHttpClientFactory httpClientFactory, ILogger<PrtgApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int?> DuplicateDeviceAsync(
        PrtgConnectionInfo connection, int templateDeviceId, string newName, string host,
        CancellationToken cancellationToken)
    {
        // PRTG: /api/duplicateobject.htm?id=<src>&name=<new>&host=<host>&targetid=<group>&apitoken=<token>
        // We use targetid 0 to keep in the same parent group; a future enhancement
        // could let the admin pick a target group.
        var url = $"/api/duplicateobject.htm?id={templateDeviceId}" +
                  $"&name={Uri.EscapeDataString(newName)}" +
                  $"&host={Uri.EscapeDataString(host)}" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";

        var (success, body) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        if (!success)
            return null;

        // PRTG echoes the new object id either as JSON (`{"objid": 123}`)
        // or as a redirect HTML page with a link to /device.htm?id=123.
        var match = IdRegex.Match(body);
        if (match.Success)
        {
            var group = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (int.TryParse(group, out var id))
                return id;
        }

        _logger.LogWarning("DuplicateDevice response did not contain an object id. Body: {Body}",
            Truncate(body, 200));
        return null;
    }

    public async Task<bool> SetObjectPropertyAsync(
        PrtgConnectionInfo connection, int objectId, string propertyName, string value,
        CancellationToken cancellationToken)
    {
        var url = $"/api/setobjectproperty.htm?id={objectId}" +
                  $"&name={Uri.EscapeDataString(propertyName)}" +
                  $"&value={Uri.EscapeDataString(value)}" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";

        var (success, _) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        return success;
    }

    public async Task<bool> ResumeAsync(PrtgConnectionInfo connection, int objectId, CancellationToken cancellationToken)
    {
        var url = $"/api/pause.htm?id={objectId}&action=1" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";
        var (success, _) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        return success;
    }

    public async Task<bool> DeleteObjectAsync(PrtgConnectionInfo connection, int objectId, CancellationToken cancellationToken)
    {
        // approve=1 skips the confirmation prompt.
        var url = $"/api/deleteobject.htm?id={objectId}&approve=1" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";
        var (success, _) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        return success;
    }

    public async Task<bool> PingAsync(PrtgConnectionInfo connection, CancellationToken cancellationToken)
    {
        // /api/getstatus.json is the canonical "is the server alive and is my token valid" call.
        var url = $"/api/getstatus.json?apitoken={Uri.EscapeDataString(connection.ApiToken)}";
        var (success, _) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        return success;
    }

    public async Task<int?> AddDeviceAsync(
        PrtgConnectionInfo connection, int groupId, string deviceName, string host,
        CancellationToken cancellationToken)
    {
        // /api/adddevice2.htm?id=<groupId>&name_=<name>&host_=<host>&apitoken=<token>
        // PRTG's "form-style" API uses the `_` suffix for property fields.
        var url = $"/api/adddevice2.htm?id={groupId}" +
                  $"&name_={Uri.EscapeDataString(deviceName)}" +
                  $"&host_={Uri.EscapeDataString(host)}" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";
        var (success, body) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        if (!success) return null;

        // adddevice2 echoes the new object id either as JSON (`{"objid": 123}`)
        // or as an HTML redirect page with a link to /device.htm?id=123.
        var match = IdRegex.Match(body);
        if (match.Success)
        {
            var group = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (int.TryParse(group, out var id))
                return id;
        }

        _logger.LogWarning("AddDevice response did not contain an object id. Body: {Body}",
            Truncate(body, 200));
        return null;
    }

    public async Task<bool> TriggerAutoDiscoveryAsync(
        PrtgConnectionInfo connection, int deviceId, CancellationToken cancellationToken)
    {
        var url = $"/api/discovernow.htm?id={deviceId}" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";
        var (success, _) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        return success;
    }

    public async Task<bool> AddSnmpCustomSensorAsync(
        PrtgConnectionInfo connection, int deviceId, string sensorName, string numericOid,
        string unit, CancellationToken cancellationToken)
    {
        // /api/addsensor5.htm — sensortype=snmpcustom, plus PRTG's `_`-suffixed
        // form fields. We feed a fully-numeric OID so no MIB resolution is
        // needed on the PRTG side.
        var url = "/api/addsensor5.htm?sensortype=snmpcustom" +
                  $"&id={deviceId}" +
                  $"&name_={Uri.EscapeDataString(sensorName)}" +
                  $"&oid_={Uri.EscapeDataString(numericOid)}" +
                  $"&unit_={Uri.EscapeDataString(unit)}" +
                  "&priority_=3&interval_=60" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";
        var (success, _) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        return success;
    }

    public async Task<bool> AddSnmpCustomStringSensorAsync(
        PrtgConnectionInfo connection, int deviceId, string sensorName, string numericOid,
        CancellationToken cancellationToken)
    {
        var url = "/api/addsensor5.htm?sensortype=snmpcustomstring" +
                  $"&id={deviceId}" +
                  $"&name_={Uri.EscapeDataString(sensorName)}" +
                  $"&oid_={Uri.EscapeDataString(numericOid)}" +
                  "&priority_=3&interval_=60" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";
        var (success, _) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        return success;
    }

    public async Task<IReadOnlyList<PrtgGroupSummary>> ListGroupsAsync(
        PrtgConnectionInfo connection, CancellationToken cancellationToken)
    {
        // /api/table.json?content=groups&columns=objid,group,probe&count=5000
        // count=5000 covers virtually any PRTG installation.
        var url = "/api/table.json?content=groups&output=json&count=5000" +
                  "&columns=objid,group,probe" +
                  $"&apitoken={Uri.EscapeDataString(connection.ApiToken)}";
        var (success, body) = await SendAsync(connection, HttpMethod.Get, url, cancellationToken);
        if (!success) return Array.Empty<PrtgGroupSummary>();

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("groups", out var groupsArray)
                || groupsArray.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<PrtgGroupSummary>();
            }

            var list = new List<PrtgGroupSummary>(groupsArray.GetArrayLength());
            foreach (var g in groupsArray.EnumerateArray())
            {
                if (!g.TryGetProperty("objid", out var idProp) || !idProp.TryGetInt32(out var id))
                    continue;
                var name = g.TryGetProperty("group", out var nameProp) ? nameProp.GetString() ?? "" : "";
                var probe = g.TryGetProperty("probe", out var probeProp) ? probeProp.GetString() : null;
                list.Add(new PrtgGroupSummary(id, name, probe));
            }
            return list;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "PRTG /api/table.json groups returned invalid JSON. Body: {Body}",
                Truncate(body, 200));
            return Array.Empty<PrtgGroupSummary>();
        }
    }

    // ─── Internals ──────────────────────────────────────────────────

    private async Task<(bool Success, string Body)> SendAsync(
        PrtgConnectionInfo connection, HttpMethod method, string urlPath, CancellationToken ct)
    {
        var client = CreateClientFor(connection);
        var url = connection.BaseUrl.TrimEnd('/') + urlPath;

        try
        {
            using var req = new HttpRequestMessage(method, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("PRTG API {Method} {Url} returned HTTP {Status}. Body: {Body}",
                    method, RedactToken(url), (int)resp.StatusCode, Truncate(body, 200));
                return (false, body);
            }

            return (true, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PRTG API {Method} {Url} threw {ExceptionType}",
                method, RedactToken(url), ex.GetType().Name);
            return (false, string.Empty);
        }
    }

    private HttpClient CreateClientFor(PrtgConnectionInfo connection)
    {
        // The encryption-aware client name routes through HttpClientFactory's
        // SocketsHttpHandler pool; the TLS verification is per-connection so
        // we use the verify/no-verify named client based on the flag.
        var name = connection.VerifyTls ? "PrtgApiVerifyTls" : "PrtgApiNoVerifyTls";
        var client = _httpClientFactory.CreateClient(name);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static string RedactToken(string url) =>
        Regex.Replace(url, @"apitoken=[^&]*", "apitoken=<redacted>");

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
