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
        json.Should().NotContain("Temporarily unavailable", "container mode delegates the page to the product container");
    }

    [Fact]
    public void Branding_Bundle_ServesBundleHtmlInline()
    {
        var json = CaddyConfigBuilder.Build(
            BrandingConfig(EdgeMaintenancePageMode.Bundle, bundleHtml: "<html>BUNDLE-PAGE</html>"), MaintState);

        json.Should().Contain("\"status_code\":503");
        json.Should().Contain("BUNDLE-PAGE");
        json.Should().NotContain("Temporarily unavailable");
    }

    [Fact]
    public void Branding_Default_RendersStandardPage()
    {
        var json = CaddyConfigBuilder.Build(BrandingConfig(EdgeMaintenancePageMode.Default), MaintState);

        json.Should().Contain("\"status_code\":503");
        json.Should().Contain("Temporarily unavailable");
    }

    [Fact]
    public void Branding_ContainerWithoutService_FallsBackToDefault()
    {
        var json = CaddyConfigBuilder.Build(
            BrandingConfig(EdgeMaintenancePageMode.Container, service: null), MaintState);

        json.Should().Contain("Temporarily unavailable", "misconfigured container mode falls back to the default page");
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

    [Fact]
    public void DefaultPage_PlannedVsUnavailable_DiffersInWording()
    {
        var config = SampleConfig(new EdgeBranding("ACME Portal", null, "support@acme.test", new[] { "de", "en" }));

        var planned = CaddyConfigBuilder.Build(config,
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Maintenance, true, null, null, null));
        var unavailable = CaddyConfigBuilder.Build(config,
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Deploying, false, null, null, null));

        planned.Should().Contain("Scheduled maintenance");
        planned.Should().Contain("ACME Portal");
        planned.Should().Contain("support@acme.test");
        unavailable.Should().Contain("Temporarily unavailable");
        unavailable.Should().NotContain("Scheduled maintenance");
    }
}
