using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Unit tests for the pure Caddy config builder: state→config mapping, /hc passthrough,
/// status JSON shape and wording driven by the maintenance flag.
/// </summary>
public class CaddyConfigBuilderTests
{
    private static EdgeConfig SampleConfig(EdgeBranding? branding = null) => EdgeConfig.Create(
        publicHostname: "app.example.com",
        publicPort: 8443,
        upstreamService: "web-bff",
        upstreamPort: 8080,
        network: "app-edge-net",
        image: "caddy:2.8.4",
        branding: branding);

    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement EdgeRoutes(string json) =>
        Root(json).GetProperty("apps").GetProperty("http").GetProperty("servers").GetProperty("edge").GetProperty("routes");

    [Fact]
    public void Build_AlwaysExposesAdminApiAndListensOnPublicPort()
    {
        var json = CaddyConfigBuilder.Build(SampleConfig(),
            new EdgeDesiredState(EdgeMode.Proxy, EdgeStatusState.Running, false, null, null, null));

        var root = Root(json);
        root.GetProperty("admin").GetProperty("listen").GetString().Should().Be("0.0.0.0:2019");

        var listen = root.GetProperty("apps").GetProperty("http").GetProperty("servers")
            .GetProperty("edge").GetProperty("listen");
        listen.EnumerateArray().Select(e => e.GetString()).Should().Contain(":8443");
    }

    [Fact]
    public void ProxyMode_ReverseProxiesToUpstream()
    {
        var json = CaddyConfigBuilder.Build(SampleConfig(),
            new EdgeDesiredState(EdgeMode.Proxy, EdgeStatusState.Running, false, null, null, null));

        json.Should().Contain("reverse_proxy");
        json.Should().Contain("web-bff:8080");
        json.Should().NotContain("status_code\":503", "proxy mode never serves the 503 maintenance page");
    }

    [Fact]
    public void MaintenanceMode_Serves503Page_ButPassesHealthThrough()
    {
        var json = CaddyConfigBuilder.Build(SampleConfig(),
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Deploying, false, null, null, null));

        json.Should().Contain("\"status_code\":503", "maintenance serves a controlled 503 page");
        // /hc and /liveness must still be reverse-proxied to the upstream.
        json.Should().Contain("reverse_proxy");
        json.Should().Contain("/hc");
        json.Should().Contain("/liveness");
        json.Should().Contain("web-bff:8080");
    }

    [Fact]
    public void MaintenanceMode_HealthRouteIsFirst_SoItWinsOverCatchAll()
    {
        var json = CaddyConfigBuilder.Build(SampleConfig(),
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Maintenance, true, null, null, null));

        var routes = EdgeRoutes(json);
        routes.GetArrayLength().Should().BeGreaterThanOrEqualTo(3);

        // First route is the health passthrough (has a path match + reverse_proxy handler).
        var first = routes[0];
        first.GetProperty("match")[0].GetProperty("path").EnumerateArray()
            .Select(p => p.GetString()).Should().Contain("/hc");
        first.GetProperty("handle")[0].GetProperty("handler").GetString().Should().Be("reverse_proxy");

        // Last route is the catch-all maintenance page (no match → applies to everything else).
        var last = routes[routes.GetArrayLength() - 1];
        last.TryGetProperty("match", out _).Should().BeFalse("the maintenance page is the unmatched catch-all");
        last.GetProperty("handle")[0].GetProperty("status_code").GetInt32().Should().Be(503);
    }

    [Fact]
    public void StatusRoute_PresentInBothModes_AtStablePath()
    {
        foreach (var mode in new[] { EdgeMode.Proxy, EdgeMode.Maintenance })
        {
            var json = CaddyConfigBuilder.Build(SampleConfig(),
                new EdgeDesiredState(mode, EdgeStatusState.Running, false, null, null, null));
            json.Should().Contain("/__status");
        }
    }

    [Fact]
    public void StatusJson_HasStableVersionedShape()
    {
        var desired = new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Maintenance,
            PlannedMaintenance: true, Reason: "DB upgrade", Until: "2026-06-22T18:00:00Z", ProductVersion: "1.2.3");

        var status = Root(CaddyConfigBuilder.BuildStatusJson(desired));

        status.GetProperty("schema").GetInt32().Should().Be(1);
        status.GetProperty("state").GetString().Should().Be("maintenance");
        status.GetProperty("reason").GetString().Should().Be("DB upgrade");
        status.GetProperty("until").GetString().Should().Be("2026-06-22T18:00:00Z");
        status.GetProperty("productVersion").GetString().Should().Be("1.2.3");
    }

    [Fact]
    public void StatusJson_DeployingState_HasNullReasonAndUntil()
    {
        var desired = new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Deploying, false, null, null, null);

        var status = Root(CaddyConfigBuilder.BuildStatusJson(desired));

        status.GetProperty("state").GetString().Should().Be("deploying");
        status.GetProperty("reason").ValueKind.Should().Be(JsonValueKind.Null);
        status.GetProperty("until").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ── Phase 2: TLS termination ────────────────────────────────────

    private static EdgeConfig TlsConfig() => EdgeConfig.Create(
        publicHostname: "app.example.com", publicPort: 443,
        upstreamService: "web-bff", upstreamPort: 8080,
        network: "app-edge-net", image: "caddy:2.8.4",
        tlsMode: EdgeTlsMode.SelfSigned);

    private static EdgeCertMaterial SampleCert() => new(
        "-----BEGIN CERTIFICATE-----\nMIIB...\n-----END CERTIFICATE-----",
        "-----BEGIN PRIVATE KEY-----\nMIIE...\n-----END PRIVATE KEY-----",
        DateTime.UtcNow.AddDays(365), "ABC123");

    [Fact]
    public void Tls_WhenCertProvided_TerminatesWithInlinePemAndDisablesAcme()
    {
        var json = CaddyConfigBuilder.Build(TlsConfig(),
            new EdgeDesiredState(EdgeMode.Proxy, EdgeStatusState.Running, false, null, null, null),
            SampleCert());

        var apps = Root(json).GetProperty("apps");

        // TLS app loads the cert inline (no ACME, no files).
        var loadPem = apps.GetProperty("tls").GetProperty("certificates").GetProperty("load_pem");
        loadPem.GetArrayLength().Should().Be(1);
        loadPem[0].GetProperty("certificate").GetString().Should().Contain("BEGIN CERTIFICATE");
        loadPem[0].GetProperty("key").GetString().Should().Contain("BEGIN PRIVATE KEY");

        var server = apps.GetProperty("http").GetProperty("servers").GetProperty("edge");
        server.GetProperty("automatic_https").GetProperty("disable").GetBoolean()
            .Should().BeTrue("the edge must never run ACME itself (RSGO owns certs)");
        server.GetProperty("tls_connection_policies").GetArrayLength().Should().Be(1);
        server.GetProperty("listen").EnumerateArray().Select(e => e.GetString()).Should().Contain(":443");
    }

    [Fact]
    public void Tls_NoCert_StaysPlainHttp()
    {
        var json = CaddyConfigBuilder.Build(TlsConfig(),
            new EdgeDesiredState(EdgeMode.Proxy, EdgeStatusState.Running, false, null, null, null),
            tls: null);

        Root(json).GetProperty("apps").TryGetProperty("tls", out _)
            .Should().BeFalse("without cert material the edge serves plain HTTP");
    }

    [Fact]
    public void Tls_ModeNoneIgnoresCert()
    {
        // Even if cert material is supplied, TlsMode.None means no termination.
        var httpConfig = EdgeConfig.Create("h", 80, "u", 8080, "n", "caddy:2.8.4", tlsMode: EdgeTlsMode.None);

        var json = CaddyConfigBuilder.Build(httpConfig,
            new EdgeDesiredState(EdgeMode.Proxy, EdgeStatusState.Running, false, null, null, null),
            SampleCert());

        Root(json).GetProperty("apps").TryGetProperty("tls", out _).Should().BeFalse();
    }

    [Fact]
    public void Tls_RenewedCert_ChangesConfig_SoReloadIsTriggered()
    {
        var state = new EdgeDesiredState(EdgeMode.Proxy, EdgeStatusState.Running, false, null, null, null);
        var first = CaddyConfigBuilder.Build(TlsConfig(), state, SampleCert());
        var renewed = CaddyConfigBuilder.Build(TlsConfig(), state, new EdgeCertMaterial(
            "-----BEGIN CERTIFICATE-----\nDIFFERENT\n-----END CERTIFICATE-----",
            "-----BEGIN PRIVATE KEY-----\nDIFFERENT\n-----END PRIVATE KEY-----",
            DateTime.UtcNow.AddDays(365), "XYZ789"));

        renewed.Should().NotBe(first, "a renewed cert changes the config so the reconciler re-pushes (reload without restart)");
    }

    // ── Phase 3: Branding contract (default → bundle → container) ────

    private static readonly EdgeDesiredState MaintState =
        new(EdgeMode.Maintenance, EdgeStatusState.Maintenance, false, null, null, "9.9.9");

    private static EdgeConfig BrandingConfig(EdgeMaintenancePageMode mode, string? service = null, string? bundleHtml = null) =>
        EdgeConfig.Create("app.example.com", 8443, "web-bff", 8080, "app-edge-net", "caddy:2.8.4",
            maintenancePageMode: mode, maintenanceContainerService: service, maintenanceContainerPort: 8090, bundleHtml: bundleHtml);

    [Fact]
    public void Branding_Container_ProxiesToMaintenanceContainer()
    {
        var json = CaddyConfigBuilder.Build(
            BrandingConfig(EdgeMaintenancePageMode.Container, service: "maint-web"), MaintState);

        var routes = EdgeRoutes(json);
        var catchAll = routes[routes.GetArrayLength() - 1];
        catchAll.GetProperty("handle")[0].GetProperty("handler").GetString().Should().Be("reverse_proxy");
        json.Should().Contain("maint-web:8090");
        json.Should().NotContain("GET /__status", "container mode delegates the page to the product container");
    }

    [Fact]
    public void Branding_Bundle_ServesBundleHtmlInline()
    {
        var json = CaddyConfigBuilder.Build(
            BrandingConfig(EdgeMaintenancePageMode.Bundle, bundleHtml: "<html>BUNDLE-PAGE</html>"), MaintState);

        json.Should().Contain("\"status_code\":503");
        json.Should().Contain("BUNDLE-PAGE");
        json.Should().NotContain("GET /__status", "bundle mode serves the manifest HTML, not the default page");
    }

    [Fact]
    public void Branding_Default_RendersStandardPage()
    {
        var json = CaddyConfigBuilder.Build(BrandingConfig(EdgeMaintenancePageMode.Default), MaintState);

        json.Should().Contain("\"status_code\":503");
        json.Should().Contain("GET /__status", "the default page renders the live status instrument panel");
    }

    [Fact]
    public void Branding_ContainerWithoutService_FallsBackToDefault()
    {
        var json = CaddyConfigBuilder.Build(
            BrandingConfig(EdgeMaintenancePageMode.Container, service: null), MaintState);

        json.Should().Contain("GET /__status", "misconfigured container mode falls back to the default page");
    }

    [Fact]
    public void Branding_StatusJsonIdenticalAcrossAllModes()
    {
        var def = CaddyConfigBuilder.Build(BrandingConfig(EdgeMaintenancePageMode.Default), MaintState);
        var bundle = CaddyConfigBuilder.Build(BrandingConfig(EdgeMaintenancePageMode.Bundle, bundleHtml: "<x/>"), MaintState);
        var container = CaddyConfigBuilder.Build(BrandingConfig(EdgeMaintenancePageMode.Container, service: "m"), MaintState);

        var expected = CaddyConfigBuilder.BuildStatusJson(MaintState);
        foreach (var cfg in new[] { def, bundle, container })
            StatusBody(cfg).Should().Be(expected, "the status contract must be identical regardless of branding stage");
    }

    private static string StatusBody(string configJson)
    {
        foreach (var route in EdgeRoutes(configJson).EnumerateArray())
        {
            if (!route.TryGetProperty("match", out var match)) continue;
            var paths = match[0].GetProperty("path").EnumerateArray().Select(p => p.GetString());
            if (paths.Contains(CaddyConfigBuilder.StatusPath))
                return route.GetProperty("handle")[0].GetProperty("body").GetString()!;
        }
        throw new Xunit.Sdk.XunitException("No /__status route found");
    }

    // ── Default maintenance page (instrument-panel redesign) ────
    // These assert on the raw HTML from RenderDefault, not the Caddy config (which
    // double-encodes the page body as a JSON string and would obscure <, >, & and ").

    [Fact]
    public void DefaultPage_MaintenanceVsDeploying_DiffersInAccentAndState()
    {
        var config = SampleConfig(new EdgeBranding("ACME Portal", null, "support@acme.test", new[] { "de", "en" }));

        var planned = EdgeMaintenancePage.RenderDefault(config,
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Maintenance, true, null, null, null));
        var deploying = EdgeMaintenancePage.RenderDefault(config,
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Deploying, false, null, null, null));

        // Branding is rendered into the page (server-side fallback + live model).
        planned.Should().Contain("ACME Portal");
        planned.Should().Contain("support@acme.test");

        // Accent colour and initial model state are driven by the status token.
        planned.Should().Contain("--accent:#FDB022", "maintenance uses the warning accent");
        planned.Should().Contain("\"state\":\"maintenance\"", "the live model is seeded with the current state");

        deploying.Should().Contain("--accent:#7592FF", "deploying uses the brand-blue accent");
        deploying.Should().NotContain("--accent:#FDB022");
        deploying.Should().Contain("\"state\":\"deploying\"");
    }

    [Fact]
    public void DefaultPage_OmitsReasonAndUntilRows_WhenDataAbsent()
    {
        // Reason is only set for planned maintenance; until is never populated yet (#436).
        var html = EdgeMaintenancePage.RenderDefault(SampleConfig(),
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Maintenance, true, null, null, "1.2.3"));

        html.Should().Contain("id=\"rowReason\" style=\"display:none", "no reason → reason row hidden");
        html.Should().Contain("id=\"rowUntil\" style=\"display:none", "until is always null today → until row hidden");
    }

    [Fact]
    public void DefaultPage_ShowsReasonRow_WhenReasonPresent()
    {
        var html = EdgeMaintenancePage.RenderDefault(SampleConfig(),
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Maintenance, true,
                Reason: "Planned database migration", Until: null, ProductVersion: "1.2.3"));

        // Reason row is visible (empty inline style) and the value is rendered server-side.
        html.Should().Contain("id=\"rowReason\" style=\"\"");
        html.Should().Contain("Planned database migration");
    }

    [Fact]
    public void DefaultPage_FallsBackToHostname_WhenProductNameMissing()
    {
        var html = EdgeMaintenancePage.RenderDefault(SampleConfig(EdgeBranding.Empty), MaintState);

        // SampleConfig's publicHostname is the fallback brand name.
        html.Should().Contain("app.example.com");
    }

    [Fact]
    public void DefaultPage_EscapesBrandingToPreventInjection()
    {
        var config = SampleConfig(new EdgeBranding("<script>x</script>", null, null, new[] { "en" }));

        var html = EdgeMaintenancePage.RenderDefault(config, MaintState);

        // Server-rendered brand name is HTML-escaped; the live model uses JSON \u escaping.
        html.Should().NotContain("<script>x</script>", "branding must never be emitted as raw markup");
        html.Should().Contain("&lt;script&gt;", "the server-rendered brand name is HTML-escaped");
        html.Should().Contain("\\u003Cscript", "the JSON model escapes angle brackets");
    }

    [Theory]
    [InlineData(new[] { "de", "en" }, "data-lang=\"de\" aria-pressed=\"true\"")]
    [InlineData(new[] { "en", "de" }, "data-lang=\"en\" aria-pressed=\"true\"")]
    public void DefaultPage_PrimaryLocaleDrivesToggle(string[] locales, string expectedPrimaryButton)
    {
        var html = EdgeMaintenancePage.RenderDefault(
            SampleConfig(new EdgeBranding("ACME", null, null, locales)), MaintState);

        html.Should().Contain(expectedPrimaryButton);
    }

    [Fact]
    public void DefaultPage_SingleLocale_HidesLanguageToggle()
    {
        var html = EdgeMaintenancePage.RenderDefault(
            SampleConfig(new EdgeBranding("ACME", null, null, new[] { "en" })), MaintState);

        html.Should().Contain("aria-label=\"Language\" style=\"display:none");
        html.Should().Contain("data-lang=\"en\"");
        html.Should().NotContain("data-lang=\"de\"");
    }

    [Fact]
    public void DefaultPage_UnknownOrEmptyLocales_FallBackToEnAndDe()
    {
        var empty = EdgeMaintenancePage.RenderDefault(
            SampleConfig(new EdgeBranding("ACME", null, null, Array.Empty<string>())), MaintState);
        var unknown = EdgeMaintenancePage.RenderDefault(
            SampleConfig(new EdgeBranding("ACME", null, null, new[] { "fr", "es" })), MaintState);

        foreach (var html in new[] { empty, unknown })
        {
            html.Should().Contain("data-lang=\"en\"");
            html.Should().Contain("data-lang=\"de\"");
            html.Should().NotContain("data-lang=\"fr\"");
        }
    }

    [Fact]
    public void DefaultPage_RendersLogoFromBranding()
    {
        var html = EdgeMaintenancePage.RenderDefault(
            SampleConfig(new EdgeBranding("ACME", "https://cdn.acme.test/logo.svg", null, new[] { "en" })), MaintState);

        html.Should().Contain("\"logoUrl\":\"https://cdn.acme.test/logo.svg\"");
    }
}
