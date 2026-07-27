using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using Xunit;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// The redeploy/upgrade-facing wrapper around the provisioner's drift check. Its job beyond
/// delegating is the cache invalidation: a recreated container only holds the bootstrap
/// maintenance config, so a stale "already pushed" cache entry would leave the product's front
/// door stuck on the maintenance page.
/// </summary>
public class EdgeSettingsReconcilerTests
{
    private readonly Mock<IEdgeProvisioner> _provisioner = new();
    private readonly Mock<IEdgeConfigCache> _cache = new();
    private readonly EdgeSettingsReconciler _sut;

    public EdgeSettingsReconcilerTests()
    {
        _sut = new EdgeSettingsReconciler(
            _provisioner.Object, _cache.Object, NullLogger<EdgeSettingsReconciler>.Instance);
    }

    [Fact]
    public async Task ApplyCreateTimeSettings_NoEdgeConfigured_DoesNotTouchDocker()
    {
        var pd = CreateDeployment(edge: null);

        var recreated = await _sut.ApplyCreateTimeSettingsAsync(pd);

        recreated.Should().BeFalse();
        _provisioner.Verify(p => p.ReconcileEdgeMssAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<EdgeConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.Invalidate(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ApplyCreateTimeSettings_ContainerRecreated_InvalidatesCachedConfig()
    {
        var pd = CreateDeployment(EdgeMssMode.Pmtu);
        SetupProvisioner(recreated: true);

        var recreated = await _sut.ApplyCreateTimeSettingsAsync(pd);

        recreated.Should().BeTrue();
        _cache.Verify(c => c.Invalidate(pd.Id.Value), Times.Once);
        _provisioner.Verify(p => p.ReconcileEdgeMssAsync(
            pd.EnvironmentId.Value.ToString(), pd.DeploymentName, pd.ProductGroupId,
            pd.EdgeConfig!, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyCreateTimeSettings_NothingRecreated_KeepsCache()
    {
        var pd = CreateDeployment(EdgeMssMode.Pmtu);
        SetupProvisioner(recreated: false);

        var recreated = await _sut.ApplyCreateTimeSettingsAsync(pd);

        recreated.Should().BeFalse();
        _cache.Verify(c => c.Invalidate(It.IsAny<Guid>()), Times.Never,
            "an untouched edge still runs the config the reconciler pushed");
    }

    [Fact]
    public async Task ApplyCreateTimeSettings_ProvisionerFails_DoesNotBreakTheCallingOperation()
    {
        var pd = CreateDeployment(EdgeMssMode.Fixed, 1360);
        _provisioner.Setup(p => p.ReconcileEdgeMssAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EdgeConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("docker unreachable"));

        var recreated = await _sut.ApplyCreateTimeSettingsAsync(pd);

        recreated.Should().BeFalse();
        _cache.Verify(c => c.Invalidate(It.IsAny<Guid>()), Times.Never,
            "the old container is still running its old config");
    }

    private void SetupProvisioner(bool recreated)
    {
        _provisioner.Setup(p => p.ReconcileEdgeMssAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EdgeConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recreated);
    }

    private static ProductDeployment CreateDeployment(EdgeMssMode? edge, int? mssValue = null)
    {
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "stacks:testproduct", "stacks:testproduct:1.0.0",
            "testproduct", "Test Product", "1.0.0",
            UserId.NewId(), "test-deploy",
            new List<StackDeploymentConfig>
            {
                new("stack-0", "Stack 0", "sid:0", 1, new Dictionary<string, string>())
            },
            new Dictionary<string, string>());

        if (edge.HasValue)
        {
            pd.SetEdgeConfig(EdgeConfig.Create(
                "app.test", 443, "bff", 8080, "edge-net", "caddy:2.8.4",
                mssMode: edge.Value, mssValue: mssValue));
        }

        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.CompleteStack("stack-0");
        return pd;
    }
}
