using System.Net.Http.Headers;
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
