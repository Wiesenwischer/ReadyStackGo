using FluentAssertions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.StackManagement.Manifests;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Unit tests for mapping the manifest <c>edge:</c> block to a resolved <see cref="EdgeConfig"/>,
/// including ${VAR} resolution, defaults, and the inert (null) cases that keep existing
/// products unaffected.
/// </summary>
public class EdgeConfigMapperTests
{
    private static readonly Dictionary<string, string> NoVars = new();

    [Fact]
    public void NullBlock_MapsToNull()
    {
        EdgeConfigMapper.Map(null, NoVars).Should().BeNull();
    }

    [Fact]
    public void DisabledBlock_MapsToNull()
    {
        var edge = new RsgoEdge { Enabled = false, PublicHostname = "x", Network = "n", Upstream = new() { Service = "s" } };
        EdgeConfigMapper.Map(edge, NoVars).Should().BeNull("an explicitly disabled edge stays inert");
    }

    [Fact]
    public void MissingMandatoryFields_MapToNull()
    {
        new RsgoEdge { Enabled = true, Network = "n", Upstream = new() { Service = "s" } }
            .Pipe(e => EdgeConfigMapper.Map(e, NoVars)).Should().BeNull("hostname missing");

        new RsgoEdge { Enabled = true, PublicHostname = "h", Upstream = new() { Service = "s" } }
            .Pipe(e => EdgeConfigMapper.Map(e, NoVars)).Should().BeNull("network missing");

        new RsgoEdge { Enabled = true, PublicHostname = "h", Network = "n" }
            .Pipe(e => EdgeConfigMapper.Map(e, NoVars)).Should().BeNull("upstream service missing");
    }

    [Fact]
    public void UnresolvedVariable_InMandatoryField_MapsToNull()
    {
        var edge = new RsgoEdge
        {
            Enabled = true,
            PublicHostname = "${MISSING_HOST}",
            Network = "edge-net",
            Upstream = new() { Service = "bff" }
        };

        EdgeConfigMapper.Map(edge, NoVars).Should().BeNull("an unresolved mandatory placeholder disables the edge");
    }

    [Fact]
    public void AppliesDefaults_ForPortsAndImage()
    {
        var edge = new RsgoEdge
        {
            Enabled = true,
            PublicHostname = "app.test",
            Network = "edge-net",
            Upstream = new() { Service = "bff" }
        };

        var config = EdgeConfigMapper.Map(edge, NoVars);

        config.Should().NotBeNull();
        config!.PublicPort.Should().Be(443);
        config.UpstreamPort.Should().Be(8080);
        config.Image.Should().Be("caddy:2.8.4");
        config.TlsMode.Should().Be(EdgeTlsMode.None);
        config.MaintenancePageMode.Should().Be(EdgeMaintenancePageMode.Default);
    }

    [Fact]
    public void ResolvesVariables_AndFullMapping()
    {
        var vars = new Dictionary<string, string>
        {
            ["PUBLIC_HOST"] = "project.customer.tld",
            ["EDGE_NET"] = "ams-project-edge-net"
        };
        var edge = new RsgoEdge
        {
            Enabled = true,
            PublicHostname = "${PUBLIC_HOST}",
            PublicPort = "443",
            Image = "caddy:2.8.4",
            Network = "${EDGE_NET}",
            Upstream = new() { Service = "web-bff", Port = "9090" },
            Tls = new() { Mode = "custom", CertRef = "my-cert" },
            MaintenancePage = new()
            {
                Mode = "default",
                Branding = new() { ProductName = "ams.project", SupportContact = "help@customer.tld", Locales = new() { "de", "en" } }
            }
        };

        var config = EdgeConfigMapper.Map(edge, vars);

        config.Should().NotBeNull();
        config!.PublicHostname.Should().Be("project.customer.tld");
        config.Network.Should().Be("ams-project-edge-net");
        config.UpstreamService.Should().Be("web-bff");
        config.UpstreamPort.Should().Be(9090);
        config.TlsMode.Should().Be(EdgeTlsMode.Custom);
        config.TlsCertRef.Should().Be("my-cert");
        config.MaintenancePageMode.Should().Be(EdgeMaintenancePageMode.Default);
        config.Branding.ProductName.Should().Be("ams.project");
        config.Branding.Locales.Should().BeEquivalentTo(new[] { "de", "en" });
    }

    [Fact]
    public void MapsContainerBranding_WithPortAndBundleHtml()
    {
        var edge = new RsgoEdge
        {
            Enabled = true,
            PublicHostname = "h",
            Network = "n",
            Upstream = new() { Service = "s" },
            MaintenancePage = new()
            {
                Mode = "container",
                Container = new() { Service = "maint-web", Port = "8090" }
            }
        };

        var config = EdgeConfigMapper.Map(edge, NoVars, bundleHtml: "<html>x</html>");

        config!.MaintenancePageMode.Should().Be(EdgeMaintenancePageMode.Container);
        config.MaintenanceContainerService.Should().Be("maint-web");
        config.MaintenanceContainerPort.Should().Be(8090);
        config.BundleHtml.Should().Be("<html>x</html>");
    }

    [Fact]
    public void ContainerPort_DefaultsTo80()
    {
        var edge = new RsgoEdge
        {
            Enabled = true, PublicHostname = "h", Network = "n", Upstream = new() { Service = "s" },
            MaintenancePage = new() { Mode = "container", Container = new() { Service = "m" } }
        };

        EdgeConfigMapper.Map(edge, NoVars)!.MaintenanceContainerPort.Should().Be(80);
    }

    [Theory]
    [InlineData("reuse", EdgeTlsMode.Reuse)]
    [InlineData("selfsigned", EdgeTlsMode.SelfSigned)]
    [InlineData("letsencrypt", EdgeTlsMode.LetsEncrypt)]
    [InlineData("nonsense", EdgeTlsMode.None)]
    [InlineData(null, EdgeTlsMode.None)]
    public void ParsesTlsMode(string? mode, EdgeTlsMode expected)
    {
        var edge = new RsgoEdge
        {
            Enabled = true,
            PublicHostname = "h",
            Network = "n",
            Upstream = new() { Service = "s" },
            Tls = mode == null ? null : new() { Mode = mode }
        };

        EdgeConfigMapper.Map(edge, NoVars)!.TlsMode.Should().Be(expected);
    }

    [Fact]
    public void Mss_DefaultsToPmtu_WhenAbsent()
    {
        var edge = new RsgoEdge { Enabled = true, PublicHostname = "h", Network = "n", Upstream = new() { Service = "s" } };

        var config = EdgeConfigMapper.Map(edge, NoVars);

        config!.MssMode.Should().Be(EdgeMssMode.Pmtu, "the edge is VPN-robust out of the box");
        config.MssValue.Should().BeNull();
    }

    [Theory]
    [InlineData("pmtu", EdgeMssMode.Pmtu)]
    [InlineData("PMTU", EdgeMssMode.Pmtu)]
    [InlineData("off", EdgeMssMode.Off)]
    [InlineData("Off", EdgeMssMode.Off)]
    [InlineData("", EdgeMssMode.Pmtu)]
    [InlineData("   ", EdgeMssMode.Pmtu)]
    public void Mss_ParsesKeywords(string value, EdgeMssMode expected)
    {
        var edge = MssEdge(value);

        var config = EdgeConfigMapper.Map(edge, NoVars);

        config!.MssMode.Should().Be(expected);
        config.MssValue.Should().BeNull("keyword modes never carry a numeric value");
    }

    [Fact]
    public void Mss_FixedNumber_MapsToFixedWithValue()
    {
        var config = EdgeConfigMapper.Map(MssEdge("1360"), NoVars);

        config!.MssMode.Should().Be(EdgeMssMode.Fixed);
        config.MssValue.Should().Be(1360);
    }

    [Fact]
    public void Mss_ResolvesVariablePlaceholder()
    {
        var config = EdgeConfigMapper.Map(MssEdge("${MSS}"), new Dictionary<string, string> { ["MSS"] = "1300" });

        config!.MssMode.Should().Be(EdgeMssMode.Fixed);
        config.MssValue.Should().Be(1300);
    }

    [Theory]
    [InlineData("535")]   // below RFC-879 minimum
    [InlineData("1461")]  // above standard-Ethernet payload
    [InlineData("0")]
    [InlineData("-100")]
    [InlineData("1400.5")]
    [InlineData("nonsense")]
    [InlineData("${UNRESOLVED}")]
    public void Mss_InvalidOrOutOfRange_FallsBackToPmtu(string value)
    {
        var config = EdgeConfigMapper.Map(MssEdge(value), NoVars);

        config!.MssMode.Should().Be(EdgeMssMode.Pmtu, "an unusable front door is worse than an ignored tuning value");
        config.MssValue.Should().BeNull();
    }

    [Theory]
    [InlineData("536")]
    [InlineData("1460")]
    public void Mss_AcceptsInclusiveBounds(string value)
    {
        var config = EdgeConfigMapper.Map(MssEdge(value), NoVars);

        config!.MssMode.Should().Be(EdgeMssMode.Fixed);
        config.MssValue.Should().Be(int.Parse(value));
    }

    private static RsgoEdge MssEdge(string? mss) => new()
    {
        Enabled = true,
        PublicHostname = "h",
        Network = "n",
        Upstream = new() { Service = "s" },
        Mss = mss
    };
}

internal static class PipeExtensions
{
    public static TOut Pipe<TIn, TOut>(this TIn input, Func<TIn, TOut> f) => f(input);
}
