using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// Service for checking application version and updates via GitHub Releases API.
/// </summary>
public class VersionCheckService : IVersionCheckService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/Wiesenwischer/ReadyStackGo/releases/latest";
    private const string CacheKey = "LatestVersionInfo";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VersionCheckService> _logger;

    public VersionCheckService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<VersionCheckService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;

        if (version != null)
        {
            // Return major.minor.patch format
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        // Fallback to informational version
        var infoVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (infoVersion != null)
        {
            // Remove commit hash suffix if present
            var plusIndex = infoVersion.IndexOf('+');
            if (plusIndex > 0)
            {
                return infoVersion[..plusIndex];
            }
            return infoVersion;
        }

        return "0.0.0";
    }

    public async Task<LatestVersionInfo?> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache.TryGetValue(CacheKey, out LatestVersionInfo? cachedInfo))
        {
            return cachedInfo;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("GitHub");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ReadyStackGo-VersionCheck/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(GitHubApiUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("GitHub API returned {StatusCode} when checking for updates",
                    response.StatusCode);
                return null;
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken);

            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                _logger.LogDebug("Invalid response from GitHub API");
                return null;
            }

            var info = new LatestVersionInfo(
                Version: release.TagName.TrimStart('v', 'V'),
                ReleaseUrl: release.HtmlUrl ?? $"https://github.com/Wiesenwischer/ReadyStackGo/releases/tag/{release.TagName}",
                PublishedAt: release.PublishedAt);

            // Cache the result
            _cache.Set(CacheKey, info, CacheDuration);

            _logger.LogDebug("Latest version from GitHub: {Version}", info.Version);
            return info;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Failed to check for updates from GitHub");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogDebug("Timeout while checking for updates from GitHub");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error while checking for updates");
            return null;
        }
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }
    }
}
