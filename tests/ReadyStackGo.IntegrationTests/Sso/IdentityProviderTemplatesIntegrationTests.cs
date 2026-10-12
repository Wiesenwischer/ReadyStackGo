using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// The identity provider template catalog through the API: built-in templates, the operator
/// directory (IdentityProviderTemplates:Path), the Enabled filter and the anonymous icon endpoint.
/// </summary>
public class IdentityProviderTemplatesIntegrationTests : IAsyncLifetime
{
    private readonly List<SsoTestContext> _contexts = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var ctx in _contexts)
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Templates_WithEmptyDirectory_ListsBuiltIns()
    {
        var (_, browser) = await StartAsync(_ => { });

        var templates = await ListAsync(browser);

        templates.Select(Id).Should().Equal("wysch", "generic-oidc");
        var wysch = templates.Single(t => Id(t) == "wysch");
        wysch.GetProperty("authorityUrl").GetString().Should().Be("https://id.wysch.wiesenwischer.de/");
        wysch.GetProperty("interaction").GetString().Should().Be("connect");
        wysch.GetProperty("requireHttps").GetBoolean().Should().BeTrue();
        wysch.GetProperty("iconUrl").GetString().Should().Be("/api/identity-provider-templates/wysch/icon");
        var generic = templates.Single(t => Id(t) == "generic-oidc");
        generic.GetProperty("hasFixedAuthority").GetBoolean().Should().BeFalse();
        generic.GetProperty("authorityHint").GetString().Should().Contain("/realms/");
        generic.GetProperty("interaction").GetString().Should().Be("manualEntry");
        generic.GetProperty("iconUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Templates_FromDirectory_AppearsAndReplacesBuiltInWithSameId()
    {
        var (_, browser) = await StartAsync(dir =>
        {
            SsoTestContext.WriteTestWyschTemplate(dir);
            SsoTestContext.WriteTemplate(dir, "acme", """
                {
                  "id": "acme",
                  "name": "Acme SSO",
                  "description": "The Acme company login",
                  "order": 50,
                  "authority": { "url": "https://login.acme.example/" },
                  "registration": { "kind": "manual" }
                }
                """, SsoTestContext.TestIcon);
        });

        var templates = await ListAsync(browser);

        templates.Select(Id).Should().Equal("wysch", "acme", "generic-oidc");
        templates.Single(t => Id(t) == "wysch").GetProperty("authorityUrl").GetString().Should().Be(TestIdentityProviderHost.Issuer);
        var acme = templates.Single(t => Id(t) == "acme");
        acme.GetProperty("name").GetString().Should().Be("Acme SSO");
        acme.GetProperty("iconUrl").GetString().Should().Be("/api/identity-provider-templates/acme/icon");
    }

    [Fact]
    public async Task Templates_InvalidTemplatesInDirectory_AreSkipped()
    {
        var (_, browser) = await StartAsync(dir =>
        {
            SsoTestContext.WriteTemplate(dir, "broken", "{ not json");
            SsoTestContext.WriteTemplate(dir, "mismatch", """{ "id": "other", "name": "X", "authority": { "url": "https://x.example/" }, "registration": { "kind": "manual" } }""");
            SsoTestContext.WriteTemplate(dir, "unknown-kind", """{ "id": "unknown-kind", "name": "X", "authority": { "url": "https://x.example/" }, "registration": { "kind": "telepathy" } }""");
            SsoTestContext.WriteTemplate(dir, "no-openid", """{ "id": "no-openid", "name": "X", "authority": { "url": "https://x.example/" }, "registration": { "kind": "manual" }, "scopes": "profile" }""");
        });

        var templates = await ListAsync(browser);

        templates.Select(Id).Should().Equal("wysch", "generic-oidc");
    }

    [Fact]
    public async Task Templates_EnabledFilter_OffersOnlyListedIds_AndIgnoresUnknownOnes()
    {
        var (_, browser) = await StartAsync(_ => { }, new Dictionary<string, string?>
        {
            ["IdentityProviderTemplates:Enabled"] = "generic-oidc, does-not-exist"
        });

        var templates = await ListAsync(browser);

        templates.Select(Id).Should().Equal("generic-oidc");
        // A filtered template is not offered anywhere: not in the wizard, no icon, no session.
        using var anonymous = _contexts[^1].NewBrowser();
        var wizard = await SsoBrowser.ExpectAsync(anonymous.GetAsync("/api/wizard/sso/templates"), HttpStatusCode.OK);
        wizard.GetArrayLength().Should().Be(0);
        (await anonymous.GetAsync("/api/identity-provider-templates/wysch/icon")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await browser.PostJsonAsync("/api/settings/oidc/setup", new { templateId = "wysch" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Templates_RequireSystemAdmin()
    {
        var (ctx, _) = await StartAsync(_ => { });
        using var anonymous = ctx.NewBrowser();

        (await anonymous.GetAsync("/api/settings/oidc/templates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Icon_IsServedAnonymouslyAsSandboxedSvg()
    {
        var (ctx, _) = await StartAsync(SsoTestContext.WriteTestWyschTemplate);
        using var anonymous = ctx.NewBrowser();

        var response = await anonymous.GetAsync("/api/identity-provider-templates/wysch/icon");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'").And.Contain("sandbox");
        response.Headers.ETag.Should().NotBeNull();
        (await response.Content.ReadAsStringAsync()).Should().Be(SsoTestContext.TestIcon);

        var cached = new HttpRequestMessage(HttpMethod.Get, "/api/identity-provider-templates/wysch/icon");
        cached.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        (await anonymous.SendAsync(cached)).StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("WYSCH")]
    [InlineData("%2E%2E")]
    [InlineData("generic-oidc")]
    public async Task Icon_UnknownInvalidOrMissing_Returns404(string id)
    {
        var (ctx, _) = await StartAsync(_ => { });
        using var anonymous = ctx.NewBrowser();

        var response = await anonymous.GetAsync($"/api/identity-provider-templates/{id}/icon");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Icon_LargerThan64KbOrNotSvg_IsNotServed()
    {
        var (ctx, _) = await StartAsync(dir =>
        {
            SsoTestContext.WriteTemplate(dir, "big", """{ "id": "big", "name": "Big", "authority": { "url": "https://x.example/" }, "registration": { "kind": "manual" } }""",
                "<svg xmlns=\"http://www.w3.org/2000/svg\">" + new string(' ', 70 * 1024) + "</svg>");
            SsoTestContext.WriteTemplate(dir, "png", """{ "id": "png", "name": "Png", "authority": { "url": "https://x.example/" }, "registration": { "kind": "manual" } }""",
                "\u0089PNG not an svg");
        });
        using var anonymous = ctx.NewBrowser();

        (await anonymous.GetAsync("/api/identity-provider-templates/big/icon")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync("/api/identity-provider-templates/png/icon")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LoginProviders_CarryTheTemplateIcon()
    {
        var (ctx, _) = await StartAsync(SsoTestContext.WriteTestWyschTemplate);
        await ctx.AddProviderAsync("wysch", TestIdentityProviderHost.PairedStaticClientId, TestIdentityProviderHost.PairedStaticClientSecret,
            registration: "pairing");
        await ctx.AddProviderAsync("oidc");
        using var anonymous = ctx.NewBrowser();

        var providers = await SsoBrowser.ExpectAsync(anonymous.GetAsync("/api/auth/oidc/providers"), HttpStatusCode.OK);

        var byName = providers.EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!);
        byName["wysch"].GetProperty("iconUrl").GetString().Should().Be("/api/identity-provider-templates/wysch/icon");
        byName["oidc"].GetProperty("iconUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task<(SsoTestContext Ctx, SsoBrowser AdminBrowser)> StartAsync(
        Action<string> writeTemplates, Dictionary<string, string?>? settings = null)
    {
        var ctx = await SsoTestContext.StartAsync(settings, writeTemplates);
        _contexts.Add(ctx);
        ctx.Browser.Token = await ctx.CreatePasswordAdminAsync();
        return (ctx, ctx.Browser);
    }

    private static async Task<List<JsonElement>> ListAsync(SsoBrowser browser) =>
        (await SsoBrowser.ExpectAsync(browser.GetAsync("/api/settings/oidc/templates"), HttpStatusCode.OK)).EnumerateArray().ToList();

    private static string Id(JsonElement template) => template.GetProperty("id").GetString()!;
}
