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
            .WithPortBinding(443, true) // TLS test listens on 443 (Caddy's https_port)
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
        var maintenanceBody = await maintenance.Content.ReadAsStringAsync();
        maintenanceBody.Should().Contain("Scheduled maintenance", "the default page renders the maintenance wording");
        maintenanceBody.Should().Contain("DB upgrade", "the server-rendered reason flows into the page");

        var statusMaint = JsonDocument.Parse(await http.GetStringAsync(CaddyConfigBuilder.StatusPath)).RootElement;
        statusMaint.GetProperty("schema").GetInt32().Should().Be(1);
        statusMaint.GetProperty("state").GetString().Should().Be("maintenance");
        statusMaint.GetProperty("reason").GetString().Should().Be("DB upgrade");

        // ── Health endpoints pass through even in maintenance ───────
        var hc = await http.GetAsync("/hc");
        hc.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "/hc is reverse-proxied to nginx (which has no /hc → 404), proving passthrough rather than the 503 page");
    }

    [SkippableFact]
    public async Task LoadConfig_TerminatesTls_AndReloadsRenewedCertWithoutRestart()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

        var adminBaseUrl = $"http://localhost:{_edge!.GetMappedPublicPort((ushort)EdgeConstants.CaddyAdminPort)}";
        var httpsPort = _edge.GetMappedPublicPort(443);

        var adminClient = new CaddyAdminClient(new SingleHttpClientFactory(), Substitute.For<ILogger<CaddyAdminClient>>());

        // HTTPS client that ignores cert trust (self-signed) — we only assert termination works.
        using var https = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        }) { BaseAddress = new Uri($"https://localhost:{httpsPort}") };

        // Terminate on 443 (Caddy's https_port) — matches the production default publicPort.
        var tlsConfig = EdgeConfig.Create("edge.test.local", 443, "upstream", 80, "n", EdgeConstants.DefaultCaddyImage,
            tlsMode: EdgeTlsMode.SelfSigned);
        var proxyState = new EdgeDesiredState(EdgeMode.Proxy, EdgeStatusState.Running, false, null, null, "1.2.3");

        // ── Terminate TLS with an RSGO-managed self-signed cert ─────
        var cert1 = SelfSigned("edge.test.local");
        (await adminClient.LoadConfigAsync(adminBaseUrl, CaddyConfigBuilder.Build(tlsConfig, proxyState, cert1)))
            .Should().BeTrue("Caddy must accept the generated TLS config");

        var resp = await https.GetAsync("/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "the edge terminates HTTPS and proxies to nginx");
        (await resp.Content.ReadAsStringAsync()).Should().Contain("nginx");

        // ── Reload a renewed cert via the admin API, no restart ─────
        var cert2 = SelfSigned("edge.test.local");
        cert2.Thumbprint.Should().NotBe(cert1.Thumbprint);
        (await adminClient.LoadConfigAsync(adminBaseUrl, CaddyConfigBuilder.Build(tlsConfig, proxyState, cert2)))
            .Should().BeTrue("Caddy reloads the renewed cert without a restart");

        var resp2 = await https.GetAsync("/");
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "HTTPS still serves after the cert was rotated live");
    }

    [SkippableFact]
    public async Task Maintenance_BrandingModes_ContainerProxiesAndBundleServesInline()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

        var adminBaseUrl = $"http://localhost:{_edge!.GetMappedPublicPort((ushort)EdgeConstants.CaddyAdminPort)}";
        var adminClient = new CaddyAdminClient(new SingleHttpClientFactory(), Substitute.For<ILogger<CaddyAdminClient>>());
        using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{_edge.GetMappedPublicPort(80)}") };

        var maint = new EdgeDesiredState(EdgeMode.Maintenance, EdgeStatusState.Maintenance, true, null, null, "1.0.0");

        // ── Container branding: proxy the catch-all to the maintenance container (nginx) ──
        var containerCfg = EdgeConfig.Create("edge.test.local", 80, "upstream", 80, "n", EdgeConstants.DefaultCaddyImage,
            maintenancePageMode: EdgeMaintenancePageMode.Container, maintenanceContainerService: "upstream", maintenanceContainerPort: 80);
        (await adminClient.LoadConfigAsync(adminBaseUrl, CaddyConfigBuilder.Build(containerCfg, maint))).Should().BeTrue();

        var viaContainer = await http.GetAsync("/");
        viaContainer.StatusCode.Should().Be(HttpStatusCode.OK, "container branding proxies the page to the maintenance container");
        (await viaContainer.Content.ReadAsStringAsync()).Should().Contain("nginx");
        // Status is still maintenance, identical contract.
        JsonDocument.Parse(await http.GetStringAsync(CaddyConfigBuilder.StatusPath)).RootElement
            .GetProperty("state").GetString().Should().Be("maintenance");

        // ── Bundle branding: serve the manifest HTML inline as a 503 ──
        var bundleCfg = EdgeConfig.Create("edge.test.local", 80, "upstream", 80, "n", EdgeConstants.DefaultCaddyImage,
            maintenancePageMode: EdgeMaintenancePageMode.Bundle, bundleHtml: "<html><body>BUNDLE-PAGE-XYZ</body></html>");
        (await adminClient.LoadConfigAsync(adminBaseUrl, CaddyConfigBuilder.Build(bundleCfg, maint))).Should().BeTrue();

        var viaBundle = await http.GetAsync("/");
        viaBundle.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await viaBundle.Content.ReadAsStringAsync()).Should().Contain("BUNDLE-PAGE-XYZ");
    }

    private static EdgeCertMaterial SelfSigned(string hostname)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            $"CN={hostname}", rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
        san.AddDnsName(hostname);
        san.AddDnsName("localhost"); // test connects via https://localhost so SNI matches
        req.CertificateExtensions.Add(san.Build());
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
        return new EdgeCertMaterial(cert.ExportCertificatePem(), rsa.ExportPkcs8PrivateKeyPem(),
            cert.NotAfter.ToUniversalTime(), cert.Thumbprint);
    }

    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
