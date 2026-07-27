using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Infrastructure.Services.Edge;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Covers drift detection for the client-facing MSS tuning: sysctls and the network MTU are
/// fixed when the edge container is created, so a changed <c>edge.mss</c> only takes effect
/// once the container is recreated. The container carries the tuning it was created with as
/// the <c>rsgo.edge.mss</c> label — that label is the fingerprint compared here.
/// </summary>
public class EdgeMssDriftTests
{
    private const string EnvironmentId = "env";
    private const string DeploymentName = "ams.project";
    private const string ContainerName = "ams-project-edge";
    private const string ExistingContainerId = "existing-edge-id";

    private static EdgeConfig Config(EdgeMssMode mode, int? value = null) => EdgeConfig.Create(
        publicHostname: "app.test",
        publicPort: 443,
        upstreamService: "bff",
        upstreamPort: 8080,
        network: "edge-net",
        image: "caddy:2.8.4",
        mssMode: mode,
        mssValue: value);

    // ---- Fingerprint -------------------------------------------------------

    [Fact]
    public void MssFingerprint_IsStableAndModeSpecific()
    {
        EdgeConstants.MssFingerprint(Config(EdgeMssMode.Pmtu)).Should().Be("pmtu");
        EdgeConstants.MssFingerprint(Config(EdgeMssMode.Fixed, 1360)).Should().Be("1360");
        EdgeConstants.MssFingerprint(Config(EdgeMssMode.Off)).Should().Be(EdgeConstants.MssFingerprintOff);
    }

    [Fact]
    public async Task EnsureEdge_LabelsCreatedContainerWithFingerprint_AndExportsItToTheStartupBanner()
    {
        var (docker, capture) = MockDocker();
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        await provisioner.EnsureEdgeAsync(EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Fixed, 1360));

        capture.Request!.Labels.Should().Contain(EdgeConstants.MssLabel, "1360");
        capture.Request.EnvironmentVariables.Should().Contain("RSGO_EDGE_MSS_MODE", "1360",
            "the container logs the configured mode next to the values it actually reads from the kernel");
    }

    // ---- No drift → no recreation -----------------------------------------

    [Theory]
    [InlineData("pmtu", EdgeMssMode.Pmtu, null)]
    [InlineData("1360", EdgeMssMode.Fixed, 1360)]
    [InlineData("off", EdgeMssMode.Off, null)]
    public async Task ReconcileEdgeMss_MatchingLabel_KeepsContainer(
        string label, EdgeMssMode mode, int? value)
    {
        var (docker, capture) = MockDocker(ExistingWithLabels(new() { [EdgeConstants.MssLabel] = label }));
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        var recreated = await provisioner.ReconcileEdgeMssAsync(
            EnvironmentId, DeploymentName, "group", Config(mode, value));

        recreated.Should().BeFalse();
        capture.Request.Should().BeNull("no container may be created when the tuning is unchanged");
        docker.Verify(d => d.RemoveContainerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileEdgeMss_LabelCasingDiffers_KeepsContainer()
    {
        var (docker, _) = MockDocker(ExistingWithLabels(new() { [EdgeConstants.MssLabel] = "PMTU" }));
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        var recreated = await provisioner.ReconcileEdgeMssAsync(
            EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Pmtu));

        recreated.Should().BeFalse();
    }

    [Fact]
    public async Task ReconcileEdgeMss_PreFeatureContainerAndOffConfigured_KeepsContainer()
    {
        // No label at all = created before the option existed = no sysctls, default MTU = "off".
        var (docker, capture) = MockDocker(ExistingWithLabels(new()));
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        var recreated = await provisioner.ReconcileEdgeMssAsync(
            EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Off));

        recreated.Should().BeFalse();
        capture.Request.Should().BeNull();
    }

    [Fact]
    public async Task ReconcileEdgeMss_NoEdgeContainerYet_DoesNothing()
    {
        var (docker, capture) = MockDocker(existing: null);
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        var recreated = await provisioner.ReconcileEdgeMssAsync(
            EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Pmtu));

        recreated.Should().BeFalse("EnsureEdgeAsync creates it with the current tuning anyway");
        capture.Request.Should().BeNull();
        docker.Verify(d => d.RemoveContainerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Drift → recreation ------------------------------------------------

    [Fact]
    public async Task ReconcileEdgeMss_PreFeatureContainerAndPmtuConfigured_Recreates()
    {
        // The exact case that made a redeploy look like a no-op before: the default pmtu tuning
        // was configured but never applied because the container predates it.
        var (docker, capture) = MockDocker(ExistingWithLabels(new()));
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        var recreated = await provisioner.ReconcileEdgeMssAsync(
            EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Pmtu));

        recreated.Should().BeTrue();
        docker.Verify(d => d.RemoveContainerAsync(
            EnvironmentId, ExistingContainerId, true, It.IsAny<CancellationToken>()), Times.Once);
        capture.Request!.Name.Should().Be(ContainerName);
        capture.Request.Sysctls.Should().Contain(EdgeConstants.TcpMtuProbingSysctl, EdgeConstants.AdaptiveMtuProbing);
        capture.Request.Labels.Should().Contain(EdgeConstants.MssLabel, "pmtu");
    }

    [Fact]
    public async Task ReconcileEdgeMss_PmtuToOff_RecreatesWithoutSysctls()
    {
        var (docker, capture) = MockDocker(ExistingWithLabels(new() { [EdgeConstants.MssLabel] = "pmtu" }));
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        var recreated = await provisioner.ReconcileEdgeMssAsync(
            EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Off));

        recreated.Should().BeTrue();
        capture.Request!.Sysctls.Should().BeEmpty();
        capture.Request.Labels.Should().Contain(EdgeConstants.MssLabel, "off");
    }

    [Fact]
    public async Task ReconcileEdgeMss_ChangedFixedValue_RecreatesAndRequestsNewNetworkMtu()
    {
        var (docker, capture) = MockDocker(ExistingWithLabels(new() { [EdgeConstants.MssLabel] = "1360" }));
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        var recreated = await provisioner.ReconcileEdgeMssAsync(
            EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Fixed, 1200));

        recreated.Should().BeTrue("a different fixed value is drift, not just a different mode");
        capture.EdgeNetworkMtu.Should().Be(1200 + EdgeConstants.MssHeaderOverhead);
        capture.Request!.Labels.Should().Contain(EdgeConstants.MssLabel, "1200");
    }

    [Fact]
    public async Task ReconcileEdgeMss_RemovesBeforeCreating()
    {
        var order = new List<string>();
        var (docker, _) = MockDocker(ExistingWithLabels(new() { [EdgeConstants.MssLabel] = "off" }));
        docker.Setup(d => d.RemoveContainerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("remove"))
            .Returns(Task.CompletedTask);
        docker.Setup(d => d.CreateAndStartContainerAsync(
                It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("create"))
            .ReturnsAsync("new-edge-id");
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        await provisioner.ReconcileEdgeMssAsync(
            EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Pmtu));

        // The container name stays taken until the old container is gone.
        order.Should().Equal(new[] { "remove", "create" });
    }

    // ---- The background loop must never restart the front door -------------

    [Fact]
    public async Task EnsureEdge_ExistingContainerWithDriftedTuning_IsLeftAlone()
    {
        var (docker, capture) = MockDocker(ExistingWithLabels(new() { [EdgeConstants.MssLabel] = "off" }));
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        await provisioner.EnsureEdgeAsync(EnvironmentId, DeploymentName, "group", Config(EdgeMssMode.Pmtu));

        capture.Request.Should().BeNull("only an explicit redeploy/upgrade may recreate the edge");
        docker.Verify(d => d.RemoveContainerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Helpers -----------------------------------------------------------

    private static ContainerDto ExistingWithLabels(Dictionary<string, string> labels) => new()
    {
        Id = ExistingContainerId,
        Name = ContainerName,
        Image = "caddy:2.8.4",
        State = "running",
        Status = "Up 3 hours",
        Labels = labels
    };

    private sealed class Capture
    {
        public CreateContainerRequest? Request { get; set; }
        public int? EdgeNetworkMtu { get; set; }
    }

    private static (Mock<IDockerService> Docker, Capture Capture) MockDocker(ContainerDto? existing = null)
    {
        var docker = new Mock<IDockerService>();
        var capture = new Capture();

        // Only the edge network is ensured through the MTU-aware overload.
        docker.Setup(d => d.EnsureNetworkAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int?, CancellationToken>((_, _, mtu, _) => capture.EdgeNetworkMtu = mtu)
            .Returns(Task.CompletedTask);

        docker.Setup(d => d.GetContainerByNameAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        docker.Setup(d => d.ImageExistsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        docker.Setup(d => d.RemoveContainerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        docker.Setup(d => d.CreateAndStartContainerAsync(
                It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, CreateContainerRequest, CancellationToken>((_, req, _) => capture.Request = req)
            .ReturnsAsync("new-edge-id");

        return (docker, capture);
    }
}
