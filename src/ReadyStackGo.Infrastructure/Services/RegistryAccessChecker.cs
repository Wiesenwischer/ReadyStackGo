using System.Net;
using System.Net.Http.Headers;
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
/// 4. If token received → public, if 401/403 → auth required
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

    public async Task<RegistryAccessLevel> CheckAccessAsync(
        string host,
        string namespacePath,
        string repository,
        CancellationToken ct = default)
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

            // 401 — try anonymous token flow
            return await TryAnonymousTokenAsync(client, response, host, namespacePath, repository, ct);
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

    private async Task<RegistryAccessLevel> TryAnonymousTokenAsync(
        HttpClient client,
        HttpResponseMessage v2Response,
        string host,
        string namespacePath,
        string repository,
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

        // For Docker Hub, namespace "library" maps to "library/{repo}"
        var scope = $"repository:{namespacePath}/{repository}:pull";
        var tokenUrl = $"{realm}?service={Uri.EscapeDataString(service ?? "")}&scope={Uri.EscapeDataString(scope)}";

        _logger.LogDebug("Requesting anonymous token: {Url}", tokenUrl);

        try
        {
            var tokenResponse = await client.GetAsync(tokenUrl, ct);

            if (tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogDebug("Anonymous token obtained for {Host}/{Ns}/{Repo} — image is public",
                    host, namespacePath, repository);
                return RegistryAccessLevel.Public;
            }

            _logger.LogDebug("Anonymous token denied ({Status}) for {Host}/{Ns}/{Repo} — auth required",
                tokenResponse.StatusCode, host, namespacePath, repository);
            return RegistryAccessLevel.AuthRequired;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Token request failed for {Host}", host);
            return RegistryAccessLevel.Unknown;
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
