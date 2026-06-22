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
/// Unit tests for the edge reconciler, including the backward-compatibility guarantee that a
/// product deployment without an edge config is never touched (no edge container, no push).
/// </summary>
public class EdgeReconcilerTests
{
    private readonly Mock<IProductDeploymentRepository> _repo = new();
    private readonly Mock<IEdgeProvisioner> _provisioner = new();
    private readonly Mock<ICaddyAdminClient> _adminClient = new();
    private readonly EdgeConfigCache _cache = new();

    private EdgeReconciler CreateSut() => new(
        _repo.Object, _provisioner.Object, _adminClient.Object, _cache,
        new Mock<ILogger<EdgeReconciler>>().Object);

    private static ProductDeployment RunningDeployment(EdgeConfig? edge)
    {
        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.NewGuid()),
            "product-group", "product-id", "test-product", "Test Product", "1.0.0",
            UserId.Create(), "test-deploy",
            new[] { new StackDeploymentConfig("s", "S", "sid", 1, new Dictionary<string, string>()) },
            new Dictionary<string, string>());

        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            deployment.StartStack(stack.StackName, DeploymentId.NewId());
            deployment.CompleteStack(stack.StackName);
        }

        deployment.SetEdgeConfig(edge);
        return deployment;
    }

    private static EdgeConfig SampleEdge() => EdgeConfig.Create(
        "app.test", 443, "web-bff", 8080, "edge-net", "caddy:2.8.4");

    [Fact]
    public async Task ProductWithoutEdgeConfig_IsNeverTouched()
    {
        _repo.Setup(r => r.GetAllActive()).Returns(new[] { RunningDeployment(edge: null) });

        await CreateSut().ReconcileAllAsync();

        _provisioner.Verify(p => p.EnsureEdgeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EdgeConfig>(), It.IsAny<CancellationToken>()),
            Times.Never, "products without an edge: block must stay completely inert");
        _adminClient.Verify(a => a.LoadConfigAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunningProductWithEdge_PushesProxyConfig()
    {
        _repo.Setup(r => r.GetAllActive()).Returns(new[] { RunningDeployment(SampleEdge()) });
        _provisioner.Setup(p => p.EnsureEdgeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EdgeConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://test-deploy-edge:2019");
        string? pushed = null;
        _adminClient.Setup(a => a.LoadConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, cfg, _) => pushed = cfg)
            .ReturnsAsync(true);

        await CreateSut().ReconcileAllAsync();

        _provisioner.Verify(p => p.EnsureEdgeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EdgeConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        pushed.Should().NotBeNull();
        pushed!.Should().Contain("reverse_proxy").And.Contain("web-bff:8080");
    }

    [Fact]
    public async Task UnchangedDesiredState_PushesOnlyOnce_AcrossCycles()
    {
        // Same deployment instance (same id) across both cycles so the cache can dedupe.
        var pd = RunningDeployment(SampleEdge());
        _repo.Setup(r => r.GetAllActive()).Returns(new[] { pd });
        _provisioner.Setup(p => p.EnsureEdgeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EdgeConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://test-deploy-edge:2019");
        _adminClient.Setup(a => a.LoadConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut();
        await sut.ReconcileAllAsync();
        await sut.ReconcileAllAsync();

        // Same product id + same desired config across cycles → cached → pushed only once.
        _adminClient.Verify(a => a.LoadConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once, "an unchanged config must not be re-pushed (connection-preserving)");
    }
}
