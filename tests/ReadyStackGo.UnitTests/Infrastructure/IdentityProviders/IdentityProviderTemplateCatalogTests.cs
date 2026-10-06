using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Infrastructure.Services.IdentityProviders;

namespace ReadyStackGo.UnitTests.Infrastructure.IdentityProviders;

public class IdentityProviderTemplateCatalogTests : IDisposable
{
    private readonly string _root;

    public IdentityProviderTemplateCatalogTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rsgo-idp-template-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
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

    private const string ValidSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\"></svg>";

    private static string TemplateJson(
        string id,
        string? name = null,
        int? order = null,
        string authority = "\"url\":\"https://idp.example.com/\"",
        string kind = "manual",
        string? scopes = null,
        string? extra = null)
    {
        var parts = new List<string>
        {
            $"\"id\":\"{id}\"",
            $"\"name\":\"{name ?? id}\"",
            $"\"authority\":{{{authority}}}",
            $"\"registration\":{{\"kind\":\"{kind}\"}}"
        };
        if (order.HasValue) parts.Add($"\"order\":{order.Value}");
        if (scopes != null) parts.Add($"\"scopes\":\"{scopes}\"");
        if (extra != null) parts.Add(extra);
        return "{" + string.Join(",", parts) + "}";
    }

    private string WriteTemplate(string folder, string? json = null, string? icon = null, byte[]? iconBytes = null)
    {
        var path = Path.Combine(_root, folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "template.json"), json ?? TemplateJson(folder));
        if (icon != null)
        {
            File.WriteAllText(Path.Combine(path, "icon.svg"), icon);
        }
        if (iconBytes != null)
        {
            File.WriteAllBytes(Path.Combine(path, "icon.svg"), iconBytes);
        }
        return path;
    }

    private IdentityProviderTemplateCatalog CreateCatalog(
        string? path = null,
        string? enabled = null,
        TimeProvider? timeProvider = null,
        bool withBuiltIn = true,
        params string[] kinds)
    {
        var options = new IdentityProviderTemplateOptions { Path = path ?? _root, Enabled = enabled };
        var methods = (kinds.Length == 0 ? new[] { RegistrationKinds.Manual, RegistrationKinds.Pairing } : kinds)
            .Select(k => (IClientRegistrationMethod)new FakeRegistrationMethod(k));
        return new IdentityProviderTemplateCatalog(
            new StaticOptionsMonitor(options),
            NullLogger<IdentityProviderTemplateCatalog>.Instance,
            methods,
            timeProvider,
            withBuiltIn ? null : typeof(FactAttribute).Assembly);
    }

    private sealed class StaticOptionsMonitor(IdentityProviderTemplateOptions value) : IOptionsMonitor<IdentityProviderTemplateOptions>
    {
        public IdentityProviderTemplateOptions CurrentValue => value;
        public IdentityProviderTemplateOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<IdentityProviderTemplateOptions, string?> listener) => null;
    }

    private sealed class FakeRegistrationMethod(string kind) : IClientRegistrationMethod
    {
        public string Kind => kind;
        public RegistrationInteraction Interaction => RegistrationInteraction.ManualEntry;

        public Task<RegistrationStart> StartAsync(RegistrationContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RegisteredClient> CompleteAsync(RegistrationContext context, string code, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    #endregion

    #region Built-in templates

    [Fact]
    public void GetTemplates_BuiltIn_ContainsWyschAndGenericOidcSortedByOrder()
    {
        var templates = CreateCatalog(path: Path.Combine(_root, "missing")).GetTemplates();

        templates.Select(t => t.Id).Should().Equal("wysch", "generic-oidc");
    }

    [Fact]
    public void GetTemplate_BuiltInWysch_HasFixedAuthorityPairingParHttpsAndIcon()
    {
        var wysch = CreateCatalog().GetTemplate("wysch");

        wysch.Should().NotBeNull();
        wysch!.Name.Should().Be("WYSCH");
        wysch.ProviderName.Should().Be("wysch");
        wysch.ProviderDisplayName.Should().Be("WYSCH");
        wysch.HasFixedAuthority.Should().BeTrue();
        wysch.AuthorityUrl.Should().Be("https://id.wysch.wiesenwischer.de/");
        wysch.RegistrationKind.Should().Be(RegistrationKinds.Pairing);
        wysch.RequirePar.Should().BeTrue();
        wysch.RequireHttps.Should().BeTrue();
        wysch.OfferInSetup.Should().BeTrue();
        wysch.Claims.DisplayName.Should().Be("nickname");
        wysch.HasIcon.Should().BeTrue();
        wysch.SetupDescription.Should().NotBe(wysch.Description);
    }

    [Fact]
    public void GetTemplate_BuiltInGenericOidc_HasAuthorityInputAndDefaults()
    {
        var generic = CreateCatalog().GetTemplate("generic-oidc");

        generic.Should().NotBeNull();
        generic!.ProviderName.Should().Be("oidc");
        generic.ProviderDisplayName.Should().BeNull();
        generic.HasFixedAuthority.Should().BeFalse();
        generic.AuthorityInput.Should().NotBeNull();
        generic.AuthorityInput!.Example.Should().NotBeNullOrEmpty();
        generic.RegistrationKind.Should().Be(RegistrationKinds.Manual);
        generic.RequirePar.Should().BeFalse();
        generic.OfferInSetup.Should().BeFalse();
        generic.HasIcon.Should().BeFalse();
        generic.SetupDescription.Should().Be(generic.Description);
        generic.Claims.Should().Be(new OidcClaimNames());
    }

    [Fact]
    public void GetIcon_BuiltInWysch_ReturnsSvg()
    {
        var icon = CreateCatalog().GetIcon("wysch");

        icon.Should().NotBeNull();
        Encoding.UTF8.GetString(icon!).Should().Contain("<svg");
    }

    [Fact]
    public void GetTemplates_BuiltInKindUnknown_SkipsThatTemplate()
    {
        var templates = CreateCatalog(kinds: [RegistrationKinds.Manual]).GetTemplates();

        templates.Select(t => t.Id).Should().Equal("generic-oidc");
    }

    [Fact]
    public void GetTemplates_NoBuiltInAndNoDirectory_ReturnsEmptyWithoutError()
    {
        var catalog = CreateCatalog(path: Path.Combine(_root, "missing"), withBuiltIn: false);

        catalog.GetTemplates().Should().BeEmpty();
        catalog.GetTemplate("wysch").Should().BeNull();
        catalog.GetIcon("wysch").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetTemplates_PathEmpty_ReturnsOnlyBuiltIn(string? path)
    {
        var options = new IdentityProviderTemplateOptions { Path = path };
        var catalog = new IdentityProviderTemplateCatalog(
            new StaticOptionsMonitor(options),
            NullLogger<IdentityProviderTemplateCatalog>.Instance,
            [new FakeRegistrationMethod("manual"), new FakeRegistrationMethod("pairing")]);

        catalog.GetTemplates().Select(t => t.Id).Should().Equal("wysch", "generic-oidc");
    }

    #endregion

    #region Operator directory

    [Fact]
    public void GetTemplates_DirectoryTemplate_IsAddedNextToBuiltIn()
    {
        WriteTemplate("keycloak", TemplateJson("keycloak", "Keycloak", order: 50));

        var templates = CreateCatalog().GetTemplates();

        templates.Select(t => t.Id).Should().Equal("wysch", "keycloak", "generic-oidc");
    }

    [Fact]
    public void GetTemplates_DirectoryTemplateWithSameId_ReplacesBuiltIn()
    {
        WriteTemplate("wysch", TemplateJson("wysch", "WYSCH Staging", order: 10,
            authority: "\"url\":\"https://id.staging.example.com/\"", kind: "pairing"));

        var catalog = CreateCatalog();

        catalog.GetTemplates().Should().ContainSingle(t => t.Id == "wysch");
        var wysch = catalog.GetTemplate("wysch")!;
        wysch.Name.Should().Be("WYSCH Staging");
        wysch.AuthorityUrl.Should().Be("https://id.staging.example.com/");
        // The replacement brings no icon, so the built-in icon is not used either.
        wysch.HasIcon.Should().BeFalse();
        catalog.GetIcon("wysch").Should().BeNull();
    }

    [Fact]
    public void GetTemplates_MissingOrderGoesLast_TiesSortedById()
    {
        WriteTemplate("zeta", TemplateJson("zeta"));
        WriteTemplate("alpha", TemplateJson("alpha"));
        WriteTemplate("first", TemplateJson("first", order: 1));

        var templates = CreateCatalog(withBuiltIn: false).GetTemplates();

        templates.Select(t => t.Id).Should().Equal("first", "alpha", "zeta");
        templates[1].Order.Should().Be(int.MaxValue);
    }

    [Fact]
    public void GetTemplates_DefaultsApplied()
    {
        WriteTemplate("plain", TemplateJson("plain", name: "  Plain  "));

        var template = CreateCatalog(withBuiltIn: false).GetTemplate("plain")!;

        template.Name.Should().Be("Plain");
        template.ProviderName.Should().Be("plain");
        template.ProviderDisplayName.Should().Be("Plain");
        template.Scopes.Should().Be(OidcProviderSettings.DefaultScopes);
        template.Claims.Should().Be(new OidcClaimNames());
        template.RequirePar.Should().BeFalse();
        template.RequireHttps.Should().BeFalse();
        template.OfferInSetup.Should().BeFalse();
        template.HelpUrl.Should().BeNull();
        template.Description.Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_JsonWithCommentsAndTrailingCommas_IsAccepted()
    {
        WriteTemplate("lenient", """
            {
              // operator comment
              "id": "lenient",
              "name": "Lenient",
              "authority": { "url": "https://idp.example.com" },
              "registration": { "kind": "manual" },
            }
            """);

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().ContainSingle(t => t.Id == "lenient");
    }

    [Fact]
    public void GetTemplates_PartialClaims_FallBackToDefaultsPerClaim()
    {
        WriteTemplate("claims", TemplateJson("claims", extra: "\"claims\":{\"displayName\":\"nickname\"}"));

        var claims = CreateCatalog(withBuiltIn: false).GetTemplate("claims")!.Claims;

        claims.DisplayName.Should().Be("nickname");
        claims.Username.Should().Be("preferred_username");
        claims.Email.Should().Be("email");
    }

    #endregion

    #region Invalid templates are skipped

    [Theory]
    [InlineData("Upper")]
    [InlineData("-dash")]
    [InlineData("with_underscore")]
    [InlineData("dots.in.name")]
    public void GetTemplates_InvalidFolderName_IsSkipped(string folder)
    {
        WriteTemplate(folder, TemplateJson(folder));
        WriteTemplate("valid");

        CreateCatalog(withBuiltIn: false).GetTemplates().Select(t => t.Id).Should().Equal("valid");
    }

    [Fact]
    public void GetTemplates_IdDiffersFromFolder_IsSkipped()
    {
        WriteTemplate("folder", TemplateJson("other"));

        var catalog = CreateCatalog(withBuiltIn: false);

        catalog.GetTemplates().Should().BeEmpty();
        catalog.GetTemplate("other").Should().BeNull();
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    public void GetTemplates_BrokenJson_IsSkipped(string json)
    {
        WriteTemplate("broken", json);
        WriteTemplate("valid");

        CreateCatalog(withBuiltIn: false).GetTemplates().Select(t => t.Id).Should().Equal("valid");
    }

    [Fact]
    public void GetTemplates_MetadataFileMissing_IsSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_NameMissing_IsSkipped()
    {
        WriteTemplate("noname", "{\"id\":\"noname\",\"authority\":{\"url\":\"https://idp.example.com\"},\"registration\":{\"kind\":\"manual\"}}");

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_UnknownRegistrationKind_IsSkipped()
    {
        WriteTemplate("dynamic", TemplateJson("dynamic", kind: "rfc7591"));

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_RegistrationKindMissing_IsSkipped()
    {
        WriteTemplate("nokind", "{\"id\":\"nokind\",\"name\":\"x\",\"authority\":{\"url\":\"https://idp.example.com\"}}");

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_RegistrationKindDiffersInCase_IsSkipped()
    {
        WriteTemplate("upperkind", TemplateJson("upperkind", kind: "Manual"));

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Theory]
    [InlineData("\"url\":\"https://idp.example.com\",\"input\":{\"hint\":\"h\"}")]
    [InlineData("")]
    [InlineData("\"url\":\"   \"")]
    public void GetTemplates_AuthorityWithBothOrNeither_IsSkipped(string authority)
    {
        WriteTemplate("auth", TemplateJson("auth", authority: authority));

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_AuthorityMissingEntirely_IsSkipped()
    {
        WriteTemplate("noauth", "{\"id\":\"noauth\",\"name\":\"x\",\"registration\":{\"kind\":\"manual\"}}");

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Theory]
    [InlineData("ftp://idp.example.com")]
    [InlineData("idp.example.com")]
    [InlineData("/relative/path")]
    public void GetTemplates_AuthorityUrlNotAbsoluteHttp_IsSkipped(string url)
    {
        WriteTemplate("badurl", TemplateJson("badurl", authority: $"\"url\":\"{url}\""));

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_AuthorityInputOnly_IsAccepted()
    {
        WriteTemplate("input", TemplateJson("input", authority: "\"input\":{\"hint\":\"Hint\",\"example\":\"https://x\"}"));

        var template = CreateCatalog(withBuiltIn: false).GetTemplate("input")!;

        template.AuthorityUrl.Should().BeNull();
        template.AuthorityInput.Should().Be(new TemplateAuthorityInput("Hint", "https://x"));
        template.ProviderDisplayName.Should().BeNull();
    }

    [Theory]
    [InlineData("profile email")]
    [InlineData("openidx profile")]
    [InlineData("OPENID profile")]
    [InlineData("")]
    public void GetTemplates_ScopesWithoutOpenid_IsSkipped(string scopes)
    {
        WriteTemplate("scopes", TemplateJson("scopes", scopes: scopes));

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_ScopesWithOpenidAnywhere_IsAccepted()
    {
        WriteTemplate("scopes", TemplateJson("scopes", scopes: " email  openid "));

        CreateCatalog(withBuiltIn: false).GetTemplate("scopes")!.Scopes.Should().Be("email  openid");
    }

    [Fact]
    public void GetTemplates_InvalidProviderName_IsSkipped()
    {
        WriteTemplate("named", TemplateJson("named", extra: "\"provider\":{\"name\":\"Bad Name\"}"));

        CreateCatalog(withBuiltIn: false).GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_InvalidTemplateInDirectory_DoesNotHideBuiltInWithSameId()
    {
        WriteTemplate("wysch", "{ broken");

        var wysch = CreateCatalog().GetTemplate("wysch");

        wysch.Should().NotBeNull();
        wysch!.AuthorityUrl.Should().Be("https://id.wysch.wiesenwischer.de/");
    }

    #endregion

    #region Enabled filter

    [Fact]
    public void GetTemplates_Enabled_FiltersToListedIds()
    {
        WriteTemplate("keycloak");

        var catalog = CreateCatalog(enabled: "wysch, keycloak");

        catalog.GetTemplates().Select(t => t.Id).Should().BeEquivalentTo("wysch", "keycloak");
        catalog.GetTemplate("generic-oidc").Should().BeNull();
    }

    [Fact]
    public void GetTemplates_EnabledWithUnknownIds_IgnoresThem()
    {
        var templates = CreateCatalog(enabled: "unknown,generic-oidc,,  ").GetTemplates();

        templates.Select(t => t.Id).Should().Equal("generic-oidc");
    }

    [Fact]
    public void GetTemplates_EnabledOnlyUnknownIds_ReturnsEmpty()
    {
        CreateCatalog(enabled: "unknown").GetTemplates().Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetTemplates_EnabledEmpty_OffersAll(string? enabled)
    {
        CreateCatalog(enabled: enabled).GetTemplates().Should().HaveCount(2);
    }

    [Fact]
    public void GetTemplates_EnabledIsCaseSensitive()
    {
        CreateCatalog(enabled: "WYSCH").GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetIcon_TemplateNotEnabled_ReturnsNull()
    {
        CreateCatalog(enabled: "generic-oidc").GetIcon("wysch").Should().BeNull();
    }

    #endregion

    #region Icons

    [Fact]
    public void GetIcon_ValidSvg_IsReturnedAndFlagged()
    {
        WriteTemplate("icon", icon: ValidSvg);

        var catalog = CreateCatalog(withBuiltIn: false);

        catalog.GetTemplate("icon")!.HasIcon.Should().BeTrue();
        Encoding.UTF8.GetString(catalog.GetIcon("icon")!).Should().Be(ValidSvg);
    }

    [Fact]
    public void GetIcon_SvgWithXmlDeclarationAndBom_IsAccepted()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>\n" + ValidSvg)).ToArray();
        WriteTemplate("xmlicon", iconBytes: bytes);

        CreateCatalog(withBuiltIn: false).GetTemplate("xmlicon")!.HasIcon.Should().BeTrue();
    }

    [Theory]
    [InlineData("<html><body>not an svg</body></html>")]
    [InlineData("<?xml version=\"1.0\"?><html/>")]
    [InlineData("PNG")]
    [InlineData("")]
    public void GetIcon_NotSvg_TemplateKeptWithoutIcon(string content)
    {
        WriteTemplate("noicon", icon: content);

        var catalog = CreateCatalog(withBuiltIn: false);

        var template = catalog.GetTemplate("noicon");
        template.Should().NotBeNull();
        template!.HasIcon.Should().BeFalse();
        catalog.GetIcon("noicon").Should().BeNull();
    }

    [Fact]
    public void GetIcon_TooLarge_TemplateKeptWithoutIcon()
    {
        var big = ValidSvg.Replace("</svg>", "<!--" + new string('x', IdentityProviderTemplateCatalog.MaxIconBytes) + "--></svg>");
        WriteTemplate("bigicon", icon: big);

        var catalog = CreateCatalog(withBuiltIn: false);

        catalog.GetTemplate("bigicon")!.HasIcon.Should().BeFalse();
        catalog.GetIcon("bigicon").Should().BeNull();
    }

    [Fact]
    public void GetIcon_ExactlyMaxSize_IsAccepted()
    {
        var padding = IdentityProviderTemplateCatalog.MaxIconBytes - Encoding.UTF8.GetByteCount(ValidSvg) - "<!---->".Length;
        var svg = ValidSvg.Replace("</svg>", "<!--" + new string('x', padding) + "--></svg>");
        Encoding.UTF8.GetByteCount(svg).Should().Be(IdentityProviderTemplateCatalog.MaxIconBytes);
        WriteTemplate("maxicon", icon: svg);

        CreateCatalog(withBuiltIn: false).GetTemplate("maxicon")!.HasIcon.Should().BeTrue();
    }

    #endregion

    #region Lookup by id

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../wysch")]
    [InlineData("WYSCH")]
    [InlineData("wysch\n")]
    public void GetTemplateAndIcon_InvalidId_ReturnNull(string? id)
    {
        var catalog = CreateCatalog();

        catalog.GetTemplate(id).Should().BeNull();
        catalog.GetIcon(id).Should().BeNull();
    }

    [Fact]
    public void GetTemplate_UnknownId_ReturnsNull()
    {
        CreateCatalog().GetTemplate("unknown").Should().BeNull();
    }

    #endregion

    #region Caching

    [Fact]
    public void GetTemplates_WithinCacheDuration_ReturnsCachedResult()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-01T10:00:00Z"));
        var catalog = CreateCatalog(timeProvider: time, withBuiltIn: false);
        catalog.GetTemplates().Should().BeEmpty();

        WriteTemplate("late");
        time.Advance(IdentityProviderTemplateCatalog.CacheDuration - TimeSpan.FromSeconds(1));

        catalog.GetTemplates().Should().BeEmpty();
    }

    [Fact]
    public void GetTemplates_AfterCacheDuration_RescansDirectory()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-01T10:00:00Z"));
        var catalog = CreateCatalog(timeProvider: time, withBuiltIn: false);
        catalog.GetTemplates().Should().BeEmpty();

        WriteTemplate("late");
        time.Advance(IdentityProviderTemplateCatalog.CacheDuration);

        catalog.GetTemplates().Select(t => t.Id).Should().Equal("late");
    }

    #endregion
}
