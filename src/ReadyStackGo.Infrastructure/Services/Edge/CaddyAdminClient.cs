using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services.Edge;

namespace ReadyStackGo.Infrastructure.Services.Edge;

/// <summary>
/// HTTP client for the Caddy admin API. Pushes a full config document to <c>/load</c>,
/// which Caddy applies atomically while preserving in-flight connections.
/// </summary>
public class CaddyAdminClient : ICaddyAdminClient
{
    /// <summary>Named <see cref="HttpClient"/> registered in DI.</summary>
    public const string HttpClientName = "EdgeAdmin";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CaddyAdminClient> _logger;

    public CaddyAdminClient(IHttpClientFactory httpClientFactory, ILogger<CaddyAdminClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> LoadConfigAsync(string adminBaseUrl, string configJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new StringContent(configJson, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"{adminBaseUrl.TrimEnd('/')}/load", content, cancellationToken);

            if (response.IsSuccessStatusCode)
                return true;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Caddy admin /load returned {StatusCode} for {AdminBaseUrl}: {Body}",
                (int)response.StatusCode, adminBaseUrl, body);
            return false;
        }
        catch (Exception ex)
        {
            // The edge may still be starting up; the reconciler retries on the next cycle.
            _logger.LogDebug(ex, "Caddy admin /load to {AdminBaseUrl} failed (edge not reachable yet?)", adminBaseUrl);
            return false;
        }
    }
}
