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
