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
}

internal static class PipeExtensions
{
    public static TOut Pipe<TIn, TOut>(this TIn input, Func<TIn, TOut> f) => f(input);
}
