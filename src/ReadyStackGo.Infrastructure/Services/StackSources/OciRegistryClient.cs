using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ReadyStackGo.Infrastructure.Services.StackSources;

/// <summary>
/// HTTP client for the Docker Registry v2 API.
/// Supports listing tags, reading manifests, and pulling blobs with Bearer token auth.
/// Handles Docker Hub special cases and pagination.
/// </summary>
public class OciRegistryClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OciRegistryClient> _logger;

    private static readonly HashSet<string> DockerHubHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "docker.io", "index.docker.io", "registry-1.docker.io", "registry.hub.docker.com"
    };

    public OciRegistryClient(
        IHttpClientFactory httpClientFactory,
        ILogger<OciRegistryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Lists all tags for a repository, with pagination support.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListTagsAsync(
        string registryHost,
        string repository,
        string? username = null,
        string? password = null,
        CancellationToken ct = default)
    {
        var v2Host = ResolveV2Host(registryHost);
        var allTags = new List<string>();
        string? nextUrl = $"https://{v2Host}/v2/{repository}/tags/list?n=100";

        while (nextUrl != null)
        {
            var response = await SendAuthenticatedAsync(HttpMethod.Get, nextUrl, registryHost, repository, username, password, ct: ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to list tags for {Host}/{Repo}: {Status}", registryHost, repository, response.StatusCode);
                break;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsProp.EnumerateArray())
                {
                    if (tag.GetString() is { } t)
                        allTags.Add(t);
                }
            }

            // Follow Link header for pagination (RFC 5988)
            nextUrl = GetNextPageUrl(response, v2Host);
        }

        _logger.LogDebug("Listed {Count} tags for {Host}/{Repo}", allTags.Count, registryHost, repository);
        return allTags;
    }

    /// <summary>
    /// Gets the manifest for a specific tag or digest.
    /// Returns the raw JSON manifest and its digest.
    /// </summary>
    public async Task<OciManifestResult?> GetManifestAsync(
        string registryHost,
        string repository,
        string reference,
        string? username = null,
        string? password = null,
        CancellationToken ct = default)
    {
        var v2Host = ResolveV2Host(registryHost);
        var url = $"https://{v2Host}/v2/{repository}/manifests/{reference}";

        var acceptHeaders = new[]
        {
            "application/vnd.oci.image.manifest.v1+json",
            "application/vnd.docker.distribution.manifest.v2+json",
            "application/vnd.docker.distribution.manifest.list.v2+json"
        };

        var response = await SendAuthenticatedAsync(HttpMethod.Get, url, registryHost, repository, username, password, acceptHeaders, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to get manifest for {Host}/{Repo}:{Ref}: {Status}",
                registryHost, repository, reference, response.StatusCode);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var digest = response.Headers.TryGetValues("Docker-Content-Digest", out var digestValues)
            ? digestValues.FirstOrDefault()
            : null;

        return new OciManifestResult(content, digest, response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Pulls a blob (layer) by digest.
    /// </summary>
    public async Task<byte[]?> PullBlobAsync(
        string registryHost,
        string repository,
        string digest,
        string? username = null,
        string? password = null,
        CancellationToken ct = default)
    {
        var v2Host = ResolveV2Host(registryHost);
        var url = $"https://{v2Host}/v2/{repository}/blobs/{digest}";

        var response = await SendAuthenticatedAsync(HttpMethod.Get, url, registryHost, repository, username, password, ct: ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to pull blob {Digest} from {Host}/{Repo}: {Status}",
                digest, registryHost, repository, response.StatusCode);
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>
    /// Sends an authenticated request using the Docker Registry v2 Bearer token flow.
    /// </summary>
    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpMethod method,
        string url,
        string registryHost,
        string repository,
        string? username,
        string? password,
        string[]? acceptHeaders = null,
        CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("OciRegistry");
        var request = new HttpRequestMessage(method, url);

        if (acceptHeaders != null)
        {
            foreach (var accept in acceptHeaders)
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        // Try without auth first
        var response = await client.SendAsync(request, ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // 401 — obtain Bearer token
        var token = await ObtainTokenAsync(client, response, registryHost, repository, username, password, ct);
        if (token == null)
            return response;

        // Retry with token
        var retryRequest = new HttpRequestMessage(method, url);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (acceptHeaders != null)
        {
            foreach (var accept in acceptHeaders)
                retryRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        return await client.SendAsync(retryRequest, ct);
    }

    /// <summary>
    /// Obtains a Bearer token from the registry auth service.
    /// </summary>
    private async Task<string?> ObtainTokenAsync(
        HttpClient client,
        HttpResponseMessage challengeResponse,
        string registryHost,
        string repository,
        string? username,
        string? password,
        CancellationToken ct)
    {
        var authHeader = challengeResponse.Headers.WwwAuthenticate.FirstOrDefault();
        if (authHeader is not { Scheme: "Bearer" } || string.IsNullOrEmpty(authHeader.Parameter))
            return null;

        var realm = ExtractParam(authHeader.Parameter, "realm");
        var service = ExtractParam(authHeader.Parameter, "service");

        if (string.IsNullOrEmpty(realm))
            return null;

        var scope = $"repository:{repository}:pull";
        var tokenUrl = $"{realm}?service={Uri.EscapeDataString(service ?? "")}&scope={Uri.EscapeDataString(scope)}";

        var tokenRequest = new HttpRequestMessage(HttpMethod.Get, tokenUrl);
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        }

        var tokenResponse = await client.SendAsync(tokenRequest, ct);
        if (!tokenResponse.IsSuccessStatusCode)
            return null;

        var content = await tokenResponse.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("token", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String)
                return tokenProp.GetString();

            if (root.TryGetProperty("access_token", out var accessProp) && accessProp.ValueKind == JsonValueKind.String)
                return accessProp.GetString();
        }
        catch (JsonException) { }

        return null;
    }

    /// <summary>
    /// Extracts the next page URL from the Link header for pagination.
    /// </summary>
    private static string? GetNextPageUrl(HttpResponseMessage response, string v2Host)
    {
        if (!response.Headers.TryGetValues("Link", out var linkValues))
            return null;

        foreach (var link in linkValues)
        {
            // Format: </v2/repo/tags/list?n=100&last=tag>; rel="next"
            if (!link.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
                continue;

            var start = link.IndexOf('<');
            var end = link.IndexOf('>');
            if (start >= 0 && end > start)
            {
                var path = link[(start + 1)..end];
                return path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? path
                    : $"https://{v2Host}{path}";
            }
        }

        return null;
    }

    private static string ResolveV2Host(string host) =>
        IsDockerHub(host) ? "registry-1.docker.io" : host;

    private static bool IsDockerHub(string host) =>
        DockerHubHosts.Contains(host);

    private static string? ExtractParam(string headerValue, string paramName)
    {
        var search = $"{paramName}=\"";
        var startIdx = headerValue.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0) return null;

        startIdx += search.Length;
        var endIdx = headerValue.IndexOf('"', startIdx);
        return endIdx < 0 ? null : headerValue[startIdx..endIdx];
    }
}

/// <summary>
/// Result of a manifest fetch operation.
/// </summary>
public record OciManifestResult(
    string Content,
    string? Digest,
    string? MediaType);
