using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for the theme package endpoints (GET /api/themes, GET /api/themes/{id}/theme.css).
/// Built-in and operator directories point to temp directories with test packages.
/// </summary>
public class ThemesEndpointsIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly string _builtIn;
    private readonly string _extra;
    private readonly List<IDisposable> _disposables = [];

    public ThemesEndpointsIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rsgo-theme-it", Guid.NewGuid().ToString("N"));
        _builtIn = Path.Combine(_root, "builtin");
        _extra = Path.Combine(_root, "extra");
        Directory.CreateDirectory(_builtIn);
        Directory.CreateDirectory(_extra);

        WritePackage(_builtIn, "turquoise", "Turquoise", 1);
        WritePackage(_builtIn, "pastel-green", "Pastel Green", 2);
        WritePackage(_builtIn, "classic", "Classic", 3);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Ignore cleanup errors
        }
    }

    private static void WritePackage(string directory, string id, string name, int order, string? css = null)
    {
        var folder = Path.Combine(directory, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "theme.json"),
            $$"""{"id":"{{id}}","name":"{{name}}","description":"{{name}} theme","order":{{order}}}""");
        File.WriteAllText(Path.Combine(folder, "theme.css"),
            css ?? $"[data-theme=\"{id}\"] {{ --rsgo-bg-page: #ffffff; }}");
    }

    private HttpClient CreateClient(string? enabled = null, string? defaultId = null, string? configPath = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Themes:BuiltInPath"] = _builtIn,
            ["Themes:Path"] = _extra
        };
        if (configPath is not null)
        {
            settings["ConfigPath"] = configPath;
        }
        if (enabled is not null)
        {
            settings["Themes:Enabled"] = enabled;
        }
        if (defaultId is not null)
        {
            settings["Themes:Default"] = defaultId;
        }

        var factory = new ThemesWebApplicationFactory(settings);
        var client = factory.CreateClient();
        _disposables.Add(client);
        _disposables.Add(factory);
        return client;
    }

    [Fact]
    public async Task GET_Themes_Anonymous_ReturnsDefaultAndPackagesInOrder()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/themes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ThemeListResponse>();
        body.Should().NotBeNull();
        body!.Default.Should().Be("turquoise");
        body.Themes.Select(t => t.Id).Should().Equal("turquoise", "pastel-green", "classic");
        body.Themes[1].Name.Should().Be("Pastel Green");
        body.Themes[1].Description.Should().Be("Pastel Green theme");
        body.Themes[1].CssUrl.Should().Be("/api/themes/pastel-green/theme.css");
    }

    [Fact]
    public async Task GET_ThemeCss_ReturnsCssWithETagAndNoCache()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/themes/classic/theme.css");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/css");
        response.Content.Headers.ContentType.CharSet.Should().Be("utf-8");
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        response.Headers.ETag.Should().NotBeNull();
        (await response.Content.ReadAsStringAsync()).Should().Contain("[data-theme=\"classic\"]");
    }

    [Fact]
    public async Task GET_ThemeCss_WithMatchingIfNoneMatch_Returns304()
    {
        var client = CreateClient();
        var first = await client.GetAsync("/api/themes/classic/theme.css");
        var etag = first.Headers.ETag;
        etag.Should().NotBeNull();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/themes/classic/theme.css");
        request.Headers.IfNoneMatch.Add(etag!);
        var second = await client.SendAsync(request);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        second.Headers.ETag.Should().Be(etag);
        (await second.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GET_ThemeCss_WithStaleIfNoneMatch_Returns200()
    {
        var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/themes/classic/theme.css");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale\""));
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_ThemeCss_DifferentThemes_HaveDifferentETags()
    {
        var client = CreateClient();

        var classic = await client.GetAsync("/api/themes/classic/theme.css");
        var turquoise = await client.GetAsync("/api/themes/turquoise/theme.css");

        classic.Headers.ETag.Should().NotBe(turquoise.Headers.ETag);
    }

    [Theory]
    [InlineData("/api/themes/unknown/theme.css")]
    [InlineData("/api/themes/Classic/theme.css")]
    [InlineData("/api/themes/..%2F..%2Fbuiltin%2Fclassic/theme.css")]
    [InlineData("/api/themes/..%2F../theme.css")]
    [InlineData("/api/themes/%2E%2E/theme.css")]
    [InlineData("/api/themes/classic%20/theme.css")]
    [InlineData("/api/themes/classic%0A/theme.css")]
    public async Task GET_ThemeCss_UnknownOrInvalidId_Returns404(string url)
    {
        var client = CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_Themes_PackageFromThemesPath_Appears()
    {
        WritePackage(_extra, "acme", "Acme", 0, css: "/* acme */");
        var client = CreateClient();

        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");
        body!.Themes.Select(t => t.Id).Should().Equal("acme", "turquoise", "pastel-green", "classic");
        body.Default.Should().Be("turquoise");

        var css = await client.GetStringAsync("/api/themes/acme/theme.css");
        css.Should().Be("/* acme */");
    }

    [Fact]
    public async Task GET_Themes_EnabledClassic_ReturnsOnlyClassicAsDefault()
    {
        var client = CreateClient(enabled: "classic");

        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");

        body!.Themes.Select(t => t.Id).Should().Equal("classic");
        body.Default.Should().Be("classic");

        var notOffered = await client.GetAsync("/api/themes/turquoise/theme.css");
        notOffered.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_Themes_DefaultConfigured_IsReturned()
    {
        var client = CreateClient(defaultId: "pastel-green");

        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");

        body!.Default.Should().Be("pastel-green");
    }

    [Fact]
    public async Task GET_Themes_NoPackages_ReturnsEmptyListAndNullDefault()
    {
        Directory.Delete(_builtIn, recursive: true);
        var client = CreateClient();

        var response = await client.GetAsync("/api/themes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ThemeListResponse>();
        body!.Themes.Should().BeEmpty();
        body.Default.Should().BeNull();
    }

    /// <summary>
    /// Creates a config directory with an rsgo.system.json, as an installation has it before the start.
    /// </summary>
    private string WriteSystemConfig(string json)
    {
        var configPath = Path.Combine(_root, "config-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(configPath);
        File.WriteAllText(Path.Combine(configPath, "rsgo.system.json"), json);
        return configPath;
    }

    private static string? ReadStoredDefaultTheme(string configPath)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(configPath, "rsgo.system.json")));
        return doc.RootElement.TryGetProperty("defaultTheme", out var value) ? value.GetString() : null;
    }

    [Fact]
    public async Task GET_Themes_ExistingInstallationFromBeforeThemes_DefaultsToClassic()
    {
        // An installation set up with an earlier version: wizard done, no default theme stored yet.
        var configPath = WriteSystemConfig("""{"wizardState":"Installed"}""");
        var client = CreateClient(configPath: configPath);

        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");

        body!.Default.Should().Be("classic");
        ReadStoredDefaultTheme(configPath).Should().Be("classic");
    }

    [Fact]
    public async Task GET_Themes_InstallationWithStoredDefault_KeepsIt()
    {
        var configPath = WriteSystemConfig("""{"wizardState":"Installed","defaultTheme":"turquoise"}""");
        var client = CreateClient(configPath: configPath);

        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");

        body!.Default.Should().Be("turquoise");
        ReadStoredDefaultTheme(configPath).Should().Be("turquoise");
    }

    [Fact]
    public async Task GET_Themes_NotYetInstalled_DefaultsToTurquoiseAndStoresNothing()
    {
        var configPath = WriteSystemConfig("""{"wizardState":"NotStarted"}""");
        var client = CreateClient(configPath: configPath);

        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");

        body!.Default.Should().Be("turquoise");
        ReadStoredDefaultTheme(configPath).Should().BeNull();
    }

    [Fact]
    public async Task GET_Themes_OperatorDefault_WinsOverExistingInstallation()
    {
        var configPath = WriteSystemConfig("""{"wizardState":"Installed"}""");
        var client = CreateClient(defaultId: "pastel-green", configPath: configPath);

        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");

        body!.Default.Should().Be("pastel-green");
    }

    [Fact]
    public async Task GET_Themes_ExistingInstallation_ClassicNotOffered_FallsBackToTurquoise()
    {
        var configPath = WriteSystemConfig("""{"wizardState":"Installed"}""");
        var client = CreateClient(enabled: "turquoise,pastel-green", configPath: configPath);

        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");

        body!.Default.Should().Be("turquoise");
    }

    [Fact]
    public async Task CompletingTheWizard_StoresTurquoiseForTheNewInstallation()
    {
        var configPath = WriteSystemConfig("""{"wizardState":"NotStarted"}""");
        var client = CreateClient(configPath: configPath);

        var admin = await client.PostAsJsonAsync("/api/wizard/admin",
            new { username = "themeadmin", email = "themeadmin@example.com", password = "TestPassword123!" });
        admin.IsSuccessStatusCode.Should().BeTrue();
        var install = await client.PostAsJsonAsync("/api/wizard/install", new { manifestPath = (string?)null });
        install.IsSuccessStatusCode.Should().BeTrue();

        ReadStoredDefaultTheme(configPath).Should().Be("turquoise");
        var body = await client.GetFromJsonAsync<ThemeListResponse>("/api/themes");
        body!.Default.Should().Be("turquoise");
    }

    private sealed class ThemesWebApplicationFactory(Dictionary<string, string?> settings) : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
        }
    }

    private sealed record ThemeListResponse(string? Default, List<ThemeDto> Themes);

    private sealed record ThemeDto(string Id, string Name, string Description, string CssUrl);
}
