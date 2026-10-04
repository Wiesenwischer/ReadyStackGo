using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Services.Themes;

namespace ReadyStackGo.UnitTests.Infrastructure.Themes;

public class ThemeCatalogTests : IDisposable
{
    private readonly string _root;
    private readonly string _builtIn;
    private readonly string _extra;

    public ThemeCatalogTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rsgo-theme-tests", Guid.NewGuid().ToString("N"));
        _builtIn = Path.Combine(_root, "builtin");
        _extra = Path.Combine(_root, "extra");
        Directory.CreateDirectory(_builtIn);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Ignore cleanup errors
        }
    }

    #region Helpers

    private static void WritePackage(
        string directory,
        string folder,
        string? id = null,
        string? name = null,
        int order = 1,
        string? css = null,
        bool writeCss = true,
        string? rawJson = null)
    {
        var path = Path.Combine(directory, folder);
        Directory.CreateDirectory(path);
        var json = rawJson ?? $$"""
            {"id":"{{id ?? folder}}","name":"{{name ?? folder}}","description":"{{folder}} description","order":{{order}}}
            """;
        File.WriteAllText(Path.Combine(path, "theme.json"), json);
        if (writeCss)
        {
            File.WriteAllText(Path.Combine(path, "theme.css"), css ?? $"[data-theme=\"{folder}\"] {{ --rsgo-bg-page: #fff; }}");
        }
    }

    private void WriteDefaultBuiltIns()
    {
        WritePackage(_builtIn, "turquoise", name: "Turquoise", order: 1);
        WritePackage(_builtIn, "pastel-green", name: "Pastel Green", order: 2);
        WritePackage(_builtIn, "classic", name: "Classic", order: 3);
    }

    private ThemeCatalog CreateCatalog(
        string? enabled = "",
        string? defaultId = "turquoise",
        string? extraPath = null,
        string? builtInPath = null,
        TimeProvider? timeProvider = null)
    {
        var options = new ThemeOptions
        {
            BuiltInPath = builtInPath ?? _builtIn,
            Path = extraPath ?? _extra,
            Enabled = enabled,
            Default = defaultId
        };
        return new ThemeCatalog(new StaticOptionsMonitor(options), NullLogger<ThemeCatalog>.Instance, timeProvider);
    }

    private sealed class StaticOptionsMonitor(ThemeOptions value) : IOptionsMonitor<ThemeOptions>
    {
        public ThemeOptions CurrentValue => value;
        public ThemeOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ThemeOptions, string?> listener) => null;
    }

    #endregion

    #region Discovery

    [Fact]
    public void GetThemes_BuiltInPackages_AreFoundSortedByOrder()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise", "pastel-green", "classic");
        result.Themes[1].Should().Be(new ThemeInfo("pastel-green", "Pastel Green", "pastel-green description", 2));
        result.DefaultId.Should().Be("turquoise");
    }

    [Fact]
    public void GetThemes_ExtraDirectoryMissing_ReturnsOnlyBuiltIn()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(extraPath: Path.Combine(_root, "does-not-exist")).GetThemes();

        result.Themes.Should().HaveCount(3);
    }

    [Fact]
    public void GetThemes_ExtraPathEmpty_ReturnsOnlyBuiltIn()
    {
        WriteDefaultBuiltIns();
        var options = new ThemeOptions { BuiltInPath = _builtIn, Path = "  ", Enabled = null, Default = null };
        var catalog = new ThemeCatalog(new StaticOptionsMonitor(options), NullLogger<ThemeCatalog>.Instance);

        var result = catalog.GetThemes();

        result.Themes.Should().HaveCount(3);
        result.DefaultId.Should().Be("turquoise");
    }

    [Fact]
    public void GetThemes_ExtraDirectoryPackageWithSameId_ReplacesBuiltIn()
    {
        WriteDefaultBuiltIns();
        WritePackage(_extra, "classic", name: "Classic Override", order: 0, css: "/* override */");

        var catalog = CreateCatalog();
        var result = catalog.GetThemes();

        result.Themes.Should().HaveCount(3);
        result.Themes[0].Should().Be(new ThemeInfo("classic", "Classic Override", "classic description", 0));
        catalog.GetThemeCss("classic").Should().Be("/* override */");
    }

    [Fact]
    public void GetThemes_ExtraDirectoryNewPackage_IsAdded()
    {
        WriteDefaultBuiltIns();
        WritePackage(_extra, "acme", order: 10);

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise", "pastel-green", "classic", "acme");
    }

    [Fact]
    public void GetThemes_BuiltInDirectoryMissing_ReturnsOnlyExtraPackages()
    {
        WritePackage(_extra, "acme");

        var result = CreateCatalog(builtInPath: Path.Combine(_root, "missing")).GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("acme");
        result.DefaultId.Should().Be("acme");
    }

    [Fact]
    public void GetThemes_NoPackages_ReturnsEmptyListAndNullDefault()
    {
        var result = CreateCatalog().GetThemes();

        result.Themes.Should().BeEmpty();
        result.DefaultId.Should().BeNull();
    }

    [Fact]
    public void GetThemes_SameOrder_SortsById()
    {
        WritePackage(_builtIn, "zeta", order: 1);
        WritePackage(_builtIn, "alpha", order: 1);

        var result = CreateCatalog(defaultId: null).GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("alpha", "zeta");
        result.DefaultId.Should().Be("alpha");
    }

    [Fact]
    public void GetThemes_MissingOptionalFields_UsesFallbacks()
    {
        WritePackage(_builtIn, "turquoise");
        WritePackage(_builtIn, "minimal", rawJson: """{"id":"minimal"}""");

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise", "minimal");
        result.Themes[1].Should().Be(new ThemeInfo("minimal", "minimal", "", int.MaxValue));
    }

    #endregion

    #region Invalid packages

    [Theory]
    [InlineData("Classic")]
    [InlineData("-classic")]
    [InlineData("class_ic")]
    [InlineData("a12345678901234567890123456789012345678901")]
    public void GetThemes_InvalidFolderName_IsSkipped(string folder)
    {
        WritePackage(_builtIn, "turquoise");
        WritePackage(_builtIn, folder, id: folder);

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise");
    }

    [Fact]
    public void GetThemes_IdDiffersFromFolder_IsSkipped()
    {
        WritePackage(_builtIn, "turquoise");
        WritePackage(_builtIn, "classic", id: "other");

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise");
    }

    [Fact]
    public void GetThemes_IdDiffersOnlyInCase_IsSkipped()
    {
        WritePackage(_builtIn, "turquoise");
        WritePackage(_builtIn, "classic", id: "Classic");

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise");
    }

    [Fact]
    public void GetThemes_MissingCss_IsSkipped()
    {
        WritePackage(_builtIn, "turquoise");
        WritePackage(_builtIn, "classic", writeCss: false);

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise");
    }

    [Fact]
    public void GetThemes_MissingThemeJson_IsSkipped()
    {
        WritePackage(_builtIn, "turquoise");
        var folder = Path.Combine(_builtIn, "classic");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "theme.css"), "/* css */");

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise");
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"id":"classic","order":"first"}""")]
    [InlineData("""{"name":"No id"}""")]
    public void GetThemes_BrokenThemeJson_IsSkipped(string rawJson)
    {
        WritePackage(_builtIn, "turquoise");
        WritePackage(_builtIn, "classic", rawJson: rawJson);

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise");
    }

    [Fact]
    public void GetThemes_InvalidExtraPackage_DoesNotReplaceValidBuiltIn()
    {
        WriteDefaultBuiltIns();
        WritePackage(_extra, "classic", writeCss: false);

        var catalog = CreateCatalog();

        catalog.GetThemes().Themes.Should().ContainSingle(t => t.Id == "classic" && t.Name == "Classic");
        catalog.GetThemeCss("classic").Should().NotBeNull();
    }

    [Fact]
    public void GetThemes_FilesInThemeDirectory_AreIgnored()
    {
        WritePackage(_builtIn, "turquoise");
        File.WriteAllText(Path.Combine(_builtIn, "theme.json"), """{"id":"themes"}""");

        var result = CreateCatalog().GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise");
    }

    #endregion

    #region Enabled and Default

    [Fact]
    public void GetThemes_EnabledFiltersThemes()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(enabled: "classic").GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("classic");
        result.DefaultId.Should().Be("classic");
    }

    [Fact]
    public void GetThemes_EnabledWithWhitespaceAndEmptyEntries_IsTrimmed()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(enabled: " classic , ,turquoise ,").GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("turquoise", "classic");
    }

    [Fact]
    public void GetThemes_EnabledWithUnknownIds_IgnoresUnknown()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(enabled: "classic,blue").GetThemes();

        result.Themes.Select(t => t.Id).Should().Equal("classic");
    }

    [Fact]
    public void GetThemes_EnabledOnlyUnknownIds_ReturnsEmptyList()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(enabled: "blue").GetThemes();

        result.Themes.Should().BeEmpty();
        result.DefaultId.Should().BeNull();
    }

    [Fact]
    public void GetThemes_EnabledIsCaseSensitive()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(enabled: "Classic").GetThemes();

        result.Themes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetThemes_EnabledEmpty_OffersAll(string? enabled)
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(enabled: enabled).GetThemes();

        result.Themes.Should().HaveCount(3);
    }

    [Fact]
    public void GetThemes_DefaultConfigured_IsUsed()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(defaultId: " pastel-green ").GetThemes();

        result.DefaultId.Should().Be("pastel-green");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("Turquoise")]
    public void GetThemes_DefaultMissingOrUnknown_FallsBackToFirstByOrder(string? defaultId)
    {
        WritePackage(_builtIn, "classic", order: 3);
        WritePackage(_builtIn, "pastel-green", order: 2);

        var result = CreateCatalog(defaultId: defaultId).GetThemes();

        result.DefaultId.Should().Be("pastel-green");
    }

    [Fact]
    public void GetThemes_DefaultNotEnabled_FallsBackToFirstOffered()
    {
        WriteDefaultBuiltIns();

        var result = CreateCatalog(enabled: "classic,pastel-green", defaultId: "turquoise").GetThemes();

        result.DefaultId.Should().Be("pastel-green");
    }

    #endregion

    #region CSS lookup

    [Fact]
    public void GetThemeCss_OfferedTheme_ReturnsContent()
    {
        WritePackage(_builtIn, "classic", css: "[data-theme=\"classic\"] { --rsgo-bg-page: #fff; }");

        var css = CreateCatalog().GetThemeCss("classic");

        css.Should().Be("[data-theme=\"classic\"] { --rsgo-bg-page: #fff; }");
    }

    [Fact]
    public void GetThemeCss_ThemeNotEnabled_ReturnsNull()
    {
        WriteDefaultBuiltIns();

        CreateCatalog(enabled: "classic").GetThemeCss("turquoise").Should().BeNull();
    }

    [Fact]
    public void GetThemeCss_InvalidPackage_ReturnsNull()
    {
        WritePackage(_builtIn, "classic", id: "other");

        CreateCatalog().GetThemeCss("classic").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("Classic")]
    [InlineData("../classic")]
    [InlineData("..")]
    [InlineData("classic/../classic")]
    [InlineData("classic\n")]
    [InlineData("classic ")]
    public void GetThemeCss_UnknownOrInvalidId_ReturnsNull(string? id)
    {
        WriteDefaultBuiltIns();

        CreateCatalog().GetThemeCss(id).Should().BeNull();
    }

    #endregion

    #region Caching

    [Fact]
    public void GetThemes_ChangesWithinCacheDuration_AreNotVisible_AfterwardsTheyAre()
    {
        WritePackage(_builtIn, "turquoise");
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var catalog = CreateCatalog(timeProvider: time);

        catalog.GetThemes().Themes.Should().HaveCount(1);

        WritePackage(_extra, "acme", order: 5);
        time.Advance(TimeSpan.FromSeconds(10));
        catalog.GetThemes().Themes.Should().HaveCount(1);

        time.Advance(ThemeCatalog.CacheDuration);
        catalog.GetThemes().Themes.Select(t => t.Id).Should().Equal("turquoise", "acme");
    }

    [Fact]
    public void GetThemeCss_CssChangedWithinCacheDuration_ReturnsNewContent()
    {
        WritePackage(_builtIn, "turquoise", css: "/* v1 */");
        var catalog = CreateCatalog(timeProvider: new FakeTimeProvider(DateTimeOffset.UtcNow));

        catalog.GetThemeCss("turquoise").Should().Be("/* v1 */");
        File.WriteAllText(Path.Combine(_builtIn, "turquoise", "theme.css"), "/* v2 */");

        catalog.GetThemeCss("turquoise").Should().Be("/* v2 */");
    }

    [Fact]
    public void GetThemeCss_CssDeletedAfterScan_ReturnsNull()
    {
        WritePackage(_builtIn, "turquoise");
        var catalog = CreateCatalog(timeProvider: new FakeTimeProvider(DateTimeOffset.UtcNow));
        catalog.GetThemes();

        File.Delete(Path.Combine(_builtIn, "turquoise", "theme.css"));

        catalog.GetThemeCss("turquoise").Should().BeNull();
    }

    #endregion

    #region ThemeId

    [Theory]
    [InlineData("a", true)]
    [InlineData("0", true)]
    [InlineData("pastel-green", true)]
    [InlineData("a123456789012345678901234567890123456789", true)]
    [InlineData("a1234567890123456789012345678901234567890", false)]
    [InlineData("-a", false)]
    [InlineData("A", false)]
    [InlineData("ä", false)]
    [InlineData("a.b", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("a\n", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ThemeId_IsValid(string? id, bool expected)
    {
        ThemeId.IsValid(id).Should().Be(expected);
    }

    #endregion
}
