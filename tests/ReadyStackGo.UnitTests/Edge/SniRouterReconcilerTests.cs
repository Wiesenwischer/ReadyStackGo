using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Unit tests for the optional SNI router reconciler, including the critical default-off
/// invariant (nothing is provisioned or pushed when the feature is disabled).
/// </summary>
public class SniRouterReconcilerTests
{
    private readonly Mock<IProductDeploymentRepository> _repo = new();
    private readonly Mock<IEdgeProvisioner> _provisioner = new();
    private readonly Mock<ICaddyAdminClient> _adminClient = new();
    private readonly EdgeConfigCache _cache = new();

    private SniRouterReconciler CreateSut(SniRouterOptions options) => new(
        options, _repo.Object, _provisioner.Object, _adminClient.Object, _cache,
        new Mock<ILogger<SniRouterReconciler>>().Object);

    private static ProductDeployment Deployment(EdgeConfig? edge, string name)
    {
        var d = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), new EnvironmentId(Guid.NewGuid()),
            "g", "p", name, "P", "1.0.0", UserId.Create(), name,
            new[] { new StackDeploymentConfig("s", "S", "sid", 1, new Dictionary<string, string>()) },
            new Dictionary<string, string>());
        foreach (var s in d.GetStacksInDeployOrder())
        {
            d.StartStack(s.StackName, DeploymentId.NewId());
            d.CompleteStack(s.StackName);
        }
        d.SetEdgeConfig(edge);
        return d;
    }

    private static EdgeConfig TlsEdge(string host) =>
        EdgeConfig.Create(host, 443, "bff", 8080, "net", "caddy:2.8.4", tlsMode: EdgeTlsMode.SelfSigned);

    [Fact]
    public async Task Disabled_DoesNothing()
    {
        _repo.Setup(r => r.GetAllActive()).Returns(new[] { Deployment(TlsEdge("a.tld"), "dep-a") });

        await CreateSut(new SniRouterOptions { Enabled = false }).ReconcileAsync();

        _provisioner.Verify(p => p.EnsureSniRouterAsync(It.IsAny<string>(), It.IsAny<SniRouterOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        _adminClient.Verify(a => a.LoadConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Enabled_ProvisionsRouterAndPushesSniRoutes_ForTlsEdgesOnly()
    {
        // One TLS edge (routed by SNI) and one plain-HTTP edge (excluded — no SNI).
        var tls = Deployment(TlsEdge("secure.tld"), "dep-secure");
        var plain = Deployment(EdgeConfig.Create("plain.tld", 80, "bff", 8080, "net", "caddy:2.8.4"), "dep-plain");

        _repo.Setup(r => r.GetAllActive()).Returns(new[] { tls, plain });
        _provisioner.Setup(p => p.EnsureSniRouterAsync(It.IsAny<string>(), It.IsAny<SniRouterOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://rsgo-sni-router:2019");
        string? pushed = null;
        _adminClient.Setup(a => a.LoadConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, cfg, _) => pushed = cfg)
            .ReturnsAsync(true);

        await CreateSut(new SniRouterOptions { Enabled = true, ListenPort = 443 }).ReconcileAsync();

        _provisioner.Verify(p => p.EnsureSniRouterAsync(It.IsAny<string>(), It.IsAny<SniRouterOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        pushed.Should().NotBeNull();
        pushed!.Should().Contain("secure.tld", "TLS edges are routed by SNI");
        pushed.Should().NotContain("plain.tld", "non-TLS edges have no SNI to route");
    }
}
