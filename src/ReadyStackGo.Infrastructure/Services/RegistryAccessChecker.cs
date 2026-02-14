using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// Checks whether a container registry allows anonymous (public) image pulls
/// using the Docker Registry v2 API token auth flow.
///
/// Flow:
/// 1. GET https://{host}/v2/ — if 200, registry is fully open (e.g., mcr.microsoft.com)
/// 2. If 401, parse Www-Authenticate header for Bearer realm + service
/// 3. Request anonymous token: GET {realm}?service={service}&amp;scope=repository:{ns}/{repo}:pull
/// 4. If token denied → auth required
/// 5. Verify token by listing tags: GET /v2/{ns}/{repo}/tags/list with Bearer token
///    If 200 → truly public, if 401/403 → auth required (token was insufficient)
/// </summary>
public class RegistryAccessChecker : IRegistryAccessChecker
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RegistryAccessChecker> _logger;

    /// <summary>
    /// Docker Hub uses a different v2 endpoint than the token realm host.
    /// </summary>
    private static readonly HashSet<string> DockerHubHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "docker.io",
        "index.docker.io",
        "registry-1.docker.io",
        "registry.hub.docker.com"
    };

    public RegistryAccessChecker(
        IHttpClientFactory httpClientFactory,
        ILogger<RegistryAccessChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<RegistryAccessLevel> CheckAccessAsync(
        string host,
        string namespacePath,
        string repository,
        CancellationToken ct = default)
    {
        return CheckAccessCoreAsync(host, namespacePath, repository, null, null, ct);
    }

    public Task<RegistryAccessLevel> CheckAccessAsync(
        string host,
        string namespacePath,
        string repository,
        string username,
        string password,
        CancellationToken ct = default)
    {
        return CheckAccessCoreAsync(host, namespacePath, repository, username, password, ct);
    }

    private async Task<RegistryAccessLevel> CheckAccessCoreAsync(
        string host,
        string namespacePath,
        string repository,
        string? username,
        string? password,
        CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RegistryCheck");

            // Docker Hub v2 API lives on registry-1.docker.io, not docker.io
            var v2Host = IsDockerHub(host) ? "registry-1.docker.io" : host;
            var v2Url = $"https://{v2Host}/v2/";

            _logger.LogDebug("Checking registry access: {Url}", v2Url);

            var response = await client.GetAsync(v2Url, ct);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _logger.LogDebug("Registry {Host} allows anonymous access (v2 returned 200)", host);
                return RegistryAccessLevel.Public;
            }

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                _logger.LogDebug("Registry {Host} returned unexpected status {Status}", host, response.StatusCode);
                return RegistryAccessLevel.Unknown;
            }

            // 401 — try token flow (anonymous or with credentials)
            return await TryTokenFlowAsync(client, response, host, namespacePath, repository, username, password, ct);
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("Registry check timed out for {Host}", host);
            return RegistryAccessLevel.Unknown;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Registry check failed for {Host}", host);
            return RegistryAccessLevel.Unknown;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error checking registry {Host}", host);
            return RegistryAccessLevel.Unknown;
        }
    }

    private async Task<RegistryAccessLevel> TryTokenFlowAsync(
        HttpClient client,
        HttpResponseMessage v2Response,
        string host,
        string namespacePath,
        string repository,
        string? username,
        string? password,
        CancellationToken ct)
    {
        // Parse Www-Authenticate: Bearer realm="...",service="...",scope="..."
        var authHeader = v2Response.Headers.WwwAuthenticate.FirstOrDefault();
        if (authHeader is not { Scheme: "Bearer" } || string.IsNullOrEmpty(authHeader.Parameter))
        {
            _logger.LogDebug("Registry {Host} returned 401 without Bearer challenge", host);
            return RegistryAccessLevel.Unknown;
        }

        var realm = ExtractParam(authHeader.Parameter, "realm");
        var service = ExtractParam(authHeader.Parameter, "service");

        if (string.IsNullOrEmpty(realm))
        {
            _logger.LogDebug("Registry {Host} Bearer challenge missing realm", host);
            return RegistryAccessLevel.Unknown;
        }

        var scope = $"repository:{namespacePath}/{repository}:pull";
        var tokenUrl = $"{realm}?service={Uri.EscapeDataString(service ?? "")}&scope={Uri.EscapeDataString(scope)}";

        var hasCredentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
        _logger.LogDebug("Requesting token: {Url} (authenticated: {HasCreds})", tokenUrl, hasCredentials);

        try
        {
            var tokenRequest = new HttpRequestMessage(HttpMethod.Get, tokenUrl);
            if (hasCredentials)
            {
                var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            }

            var tokenResponse = await client.SendAsync(tokenRequest, ct);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogDebug("Anonymous token denied ({Status}) for {Host}/{Ns}/{Repo} — auth required",
                    tokenResponse.StatusCode, host, namespacePath, repository);
                return RegistryAccessLevel.AuthRequired;
            }

            // Token obtained — but some registries (notably Docker Hub) hand out tokens
            // for ANY repo, even private ones. The token just won't have pull access.
            // We must verify by actually using the token against the registry.
            var token = await ExtractTokenAsync(tokenResponse);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("Could not extract token from response for {Host}", host);
                return RegistryAccessLevel.Unknown;
            }

            return await VerifyTokenAccessAsync(client, host, namespacePath, repository, token, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Token request failed for {Host}", host);
            return RegistryAccessLevel.Unknown;
        }
    }

    private async Task<RegistryAccessLevel> VerifyTokenAccessAsync(
        HttpClient client,
        string host,
        string namespacePath,
        string repository,
        string token,
        CancellationToken ct)
    {
        // Use the anonymous token to list tags — if it works, the repo is truly public
        var v2Host = IsDockerHub(host) ? "registry-1.docker.io" : host;
        var tagsUrl = $"https://{v2Host}/v2/{namespacePath}/{repository}/tags/list?n=1";

        var request = new HttpRequestMessage(HttpMethod.Get, tagsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogDebug("Verifying anonymous access: {Url}", tagsUrl);

        var response = await client.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Anonymous pull verified for {Host}/{Ns}/{Repo} — truly public",
                host, namespacePath, repository);
            return RegistryAccessLevel.Public;
        }

        _logger.LogDebug("Anonymous token rejected ({Status}) for {Host}/{Ns}/{Repo} — auth required",
            response.StatusCode, host, namespacePath, repository);
        return RegistryAccessLevel.AuthRequired;
    }

    private static async Task<string?> ExtractTokenAsync(HttpResponseMessage tokenResponse)
    {
        var content = await tokenResponse.Content.ReadAsStringAsync();

        // Token responses are JSON: {"token":"..."} or {"access_token":"..."}
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("token", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String)
                return tokenProp.GetString();

            if (root.TryGetProperty("access_token", out var accessProp) && accessProp.ValueKind == JsonValueKind.String)
                return accessProp.GetString();

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractParam(string headerValue, string paramName)
    {
        // Parse: realm="https://auth.docker.io/token",service="registry.docker.io"
        var search = $"{paramName}=\"";
        var startIdx = headerValue.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0)
            return null;

        startIdx += search.Length;
        var endIdx = headerValue.IndexOf('"', startIdx);
        if (endIdx < 0)
            return null;

        return headerValue[startIdx..endIdx];
    }

    private static bool IsDockerHub(string host)
    {
        return DockerHubHosts.Contains(host);
    }
}
