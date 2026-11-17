using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Infrastructure.Services.Edge;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Covers the client-facing MSS ("VPN robustness") option end to end: domain validation on
/// <see cref="EdgeConfig"/>, the pure <see cref="EdgeProvisioner.ResolveMssTuning"/> derivation,
/// and the wiring that turns the resolved tuning into concrete Docker sysctls / a network MTU.
/// </summary>
public class EdgeMssTuningTests
{
    private static EdgeConfig Config(EdgeMssMode mode, int? value = null) => EdgeConfig.Create(
        publicHostname: "app.test",
        publicPort: 443,
        upstreamService: "bff",
        upstreamPort: 8080,
        network: "edge-net",
        image: "caddy:2.8.4",
        mssMode: mode,
        mssValue: value);

    // ---- Domain validation -------------------------------------------------

    [Fact]
    public void Create_DefaultsToPmtu()
    {
        var config = EdgeConfig.Create("h", 443, "s", 8080, "n", "img");

        config.MssMode.Should().Be(EdgeMssMode.Pmtu);
        config.MssValue.Should().BeNull();
    }

    [Theory]
    [InlineData(535)]
    [InlineData(1461)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_FixedOutOfRange_Throws(int mss)
    {
        var act = () => Config(EdgeMssMode.Fixed, mss);

        act.Should().Throw<ArgumentException>().WithParameterName("mssValue");
    }

    [Fact]
    public void Create_FixedWithoutValue_Throws()
    {
        var act = () => Config(EdgeMssMode.Fixed, null);

        act.Should().Throw<ArgumentException>().WithParameterName("mssValue");
    }

    [Theory]
    [InlineData(EdgeMssMode.Pmtu)]
    [InlineData(EdgeMssMode.Off)]
    public void Create_ValueWithoutFixedMode_Throws(EdgeMssMode mode)
    {
        var act = () => Config(mode, 1360);

        act.Should().Throw<ArgumentException>().WithParameterName("mssValue");
    }

    [Theory]
    [InlineData(536)]
    [InlineData(1360)]
    [InlineData(1460)]
    public void Create_FixedInRange_Succeeds(int mss)
    {
        Config(EdgeMssMode.Fixed, mss).MssValue.Should().Be(mss);
    }

    // ---- Tuning derivation -------------------------------------------------

    [Fact]
    public void ResolveMssTuning_Pmtu_EnablesAdaptiveSysctls_NoMtu()
    {
        var (sysctls, mtu) = EdgeProvisioner.ResolveMssTuning(Config(EdgeMssMode.Pmtu));

        sysctls.Should().Contain(EdgeConstants.TcpMtuProbingSysctl, EdgeConstants.AdaptiveMtuProbing);
        sysctls.Should().Contain(EdgeConstants.TcpBaseMssSysctl, EdgeConstants.AdaptiveBaseMss);
        mtu.Should().BeNull("adaptive mode never touches the network MTU");
    }

    [Fact]
    public void ResolveMssTuning_Fixed_SetsNetworkMtu_NoSysctls()
    {
        var (sysctls, mtu) = EdgeProvisioner.ResolveMssTuning(Config(EdgeMssMode.Fixed, 1360));

        mtu.Should().Be(1360 + EdgeConstants.MssHeaderOverhead, "MTU = MSS + IPv4/TCP headers");
        sysctls.Should().BeEmpty("the network MTU is the hard cap; no sysctl is needed");
    }

    [Fact]
    public void ResolveMssTuning_Off_ChangesNothing()
    {
        var (sysctls, mtu) = EdgeProvisioner.ResolveMssTuning(Config(EdgeMssMode.Off));

        sysctls.Should().BeEmpty();
        mtu.Should().BeNull("'off' must be byte-for-byte the pre-feature behaviour");
    }

    // ---- End-to-end wiring through the provisioner -------------------------

    [Fact]
    public async Task EnsureEdge_Pmtu_PassesSysctlsToContainer_AndDefaultNetworkMtu()
    {
        var (docker, capture) = MockDocker();
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        await provisioner.EnsureEdgeAsync("env", "ams.project", "group", Config(EdgeMssMode.Pmtu));

        capture.Request!.Sysctls.Should().Contain(EdgeConstants.TcpMtuProbingSysctl, EdgeConstants.AdaptiveMtuProbing);
        capture.EdgeNetworkMtu.Should().BeNull();
    }

    [Fact]
    public async Task EnsureEdge_Fixed_CreatesEdgeNetworkWithMtu_AndNoSysctls()
    {
        var (docker, capture) = MockDocker();
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        await provisioner.EnsureEdgeAsync("env", "ams.project", "group", Config(EdgeMssMode.Fixed, 1360));

        capture.EdgeNetworkMtu.Should().Be(1400);
        capture.Request!.Sysctls.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureEdge_Off_SetsNeitherSysctlsNorMtu()
    {
        var (docker, capture) = MockDocker();
        var provisioner = new EdgeProvisioner(docker.Object, NullLogger<EdgeProvisioner>.Instance);

        await provisioner.EnsureEdgeAsync("env", "ams.project", "group", Config(EdgeMssMode.Off));

        capture.Request!.Sysctls.Should().BeEmpty();
        capture.EdgeNetworkMtu.Should().BeNull();
    }

    private sealed class Capture
    {
        public CreateContainerRequest? Request { get; set; }
        public int? EdgeNetworkMtu { get; set; }
    }

    private static (Mock<IDockerService> Docker, Capture Capture) MockDocker()
    {
        var docker = new Mock<IDockerService>();
        var capture = new Capture();

        // The MTU-aware overload is only invoked for the edge network in this flow.
        docker.Setup(d => d.EnsureNetworkAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int?, CancellationToken>((_, _, mtu, _) => capture.EdgeNetworkMtu = mtu)
            .Returns(Task.CompletedTask);

        // No pre-existing container → the provisioner creates one (Moq returns null by default).
        docker.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        docker.Setup(d => d.CreateAndStartContainerAsync(
                It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, CreateContainerRequest, CancellationToken>((_, req, _) => capture.Request = req)
            .ReturnsAsync("edge-container-id");

        return (docker, capture);
    }
}
