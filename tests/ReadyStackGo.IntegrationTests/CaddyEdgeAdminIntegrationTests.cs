using System.Net;
using System.Net.Http;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Infrastructure.Services.Edge;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Docker integration test for the Caddy admin API config-push flow: switching an edge between
/// proxy and maintenance via <c>POST /load</c> without a container restart, and verifying the
/// resulting routing behaviour (real upstream proxy, controlled 503 page, stable status JSON).
/// </summary>
[Trait("Category", "Docker")]
[Collection("Docker")]
public class CaddyEdgeAdminIntegrationTests : IAsyncLifetime
{
    private readonly DockerTestFixture _fixture;
    private INetwork? _network;
    private IContainer? _upstream;
    private IContainer? _edge;

    private static readonly EdgeConfig Config = EdgeConfig.Create(
        publicHostname: "edge.test.local",
        publicPort: 80,
        upstreamService: "upstream",
        upstreamPort: 80,
        network: "ignored-at-runtime",
        image: EdgeConstants.DefaultCaddyImage);

    public CaddyEdgeAdminIntegrationTests(DockerTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        if (!_fixture.IsDockerAvailable) return;

        _network = new NetworkBuilder().WithName($"edge-it-{Guid.NewGuid():N}").Build();
        await _network.CreateAsync();

        // Internal-only upstream (reached by the edge over the docker network via its alias).
        // Default wait (container running) — no host HTTP probe needed.
        _upstream = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithNetwork(_network)
            .WithNetworkAliases("upstream")
            .Build();
        await _upstream.StartAsync();

        // Bootstrap config: admin on all interfaces + a minimal maintenance server, so the
        // admin API is reachable immediately and we can push the live config via /load.
        var bootstrap = CaddyConfigBuilder.Build(Config,
            new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Deploying, false, null, null, null));

        _edge = new ContainerBuilder()
            .WithImage(EdgeConstants.DefaultCaddyImage)
            .WithNetwork(_network)
            // Mirror the EdgeProvisioner bootstrap exactly: pass the config via env var and an
            // sh entrypoint that writes it and runs caddy (the caddy image has no ENTRYPOINT).
            .WithEnvironment("CADDY_BOOTSTRAP_CONFIG", bootstrap)
            .WithEntrypoint("sh", "-c",
                "printf '%s' \"$CADDY_BOOTSTRAP_CONFIG\" > /etc/caddy/bootstrap.json && exec caddy run --config /etc/caddy/bootstrap.json")
            .WithPortBinding(80, true)
            .WithPortBinding(EdgeConstants.CaddyAdminPort, true)
            .Build();
        await _edge.StartAsync();

        // Robust readiness: poll the Caddy admin API ourselves (avoids flaky Testcontainers
        // HTTP/log wait-strategy timing on this host).
        await WaitForAdminReadyAsync();
    }

    private async Task WaitForAdminReadyAsync()
    {
        var adminBaseUrl = $"http://localhost:{_edge!.GetMappedPublicPort((ushort)EdgeConstants.CaddyAdminPort)}";
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var i = 0; i < 30; i++)
        {
            try
            {
                var resp = await probe.GetAsync($"{adminBaseUrl}/config/");
                if (resp.IsSuccessStatusCode) return;
            }
            catch { /* not up yet */ }
            await Task.Delay(500);
        }

        var (stdout, stderr) = await _edge.GetLogsAsync();
        throw new InvalidOperationException(
            $"Caddy admin API at {adminBaseUrl} did not become ready.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    public async Task DisposeAsync()
    {
        if (_edge != null) await _edge.DisposeAsync();
        if (_upstream != null) await _upstream.DisposeAsync();
        if (_network != null) await _network.DeleteAsync();
    }

    [SkippableFact]
    public async Task LoadConfig_SwitchesBetweenProxyAndMaintenance_WithoutRestart()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

        var adminBaseUrl = $"http://localhost:{_edge!.GetMappedPublicPort((ushort)EdgeConstants.CaddyAdminPort)}";
        var publicBaseUrl = $"http://localhost:{_edge.GetMappedPublicPort(80)}";

        var adminClient = new CaddyAdminClient(new SingleHttpClientFactory(), Substitute.For<ILogger<CaddyAdminClient>>());
        using var http = new HttpClient { BaseAddress = new Uri(publicBaseUrl) };

        // ── Proxy mode ──────────────────────────────────────────────
        var proxyState = new EdgeDesiredState(EdgeMode.Proxy, EdgeStatusState.Running, false, null, null, "1.2.3");
        (await adminClient.LoadConfigAsync(adminBaseUrl, CaddyConfigBuilder.Build(Config, proxyState)))
            .Should().BeTrue("Caddy must accept the generated proxy config");

        var proxied = await http.GetAsync("/");
        proxied.StatusCode.Should().Be(HttpStatusCode.OK, "proxy mode forwards to the nginx upstream");
        (await http.GetStringAsync("/")).Should().Contain("nginx");

        var statusRunning = JsonDocument.Parse(await http.GetStringAsync(CaddyConfigBuilder.StatusPath)).RootElement;
        statusRunning.GetProperty("state").GetString().Should().Be("running");
        statusRunning.GetProperty("productVersion").GetString().Should().Be("1.2.3");

        // ── Maintenance mode (same container, no restart) ───────────
        var maintState = new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Maintenance, true, "DB upgrade", null, "1.2.3");
        (await adminClient.LoadConfigAsync(adminBaseUrl, CaddyConfigBuilder.Build(Config, maintState)))
            .Should().BeTrue("Caddy must accept the generated maintenance config");

        var maintenance = await http.GetAsync("/");
        maintenance.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "maintenance serves a controlled 503");
        (await maintenance.Content.ReadAsStringAsync()).Should().Contain("Scheduled maintenance");

        var statusMaint = JsonDocument.Parse(await http.GetStringAsync(CaddyConfigBuilder.StatusPath)).RootElement;
        statusMaint.GetProperty("schema").GetInt32().Should().Be(1);
        statusMaint.GetProperty("state").GetString().Should().Be("maintenance");
        statusMaint.GetProperty("reason").GetString().Should().Be("DB upgrade");

        // ── Health endpoints pass through even in maintenance ───────
        var hc = await http.GetAsync("/hc");
        hc.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "/hc is reverse-proxied to nginx (which has no /hc → 404), proving passthrough rather than the 503 page");
    }

    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
