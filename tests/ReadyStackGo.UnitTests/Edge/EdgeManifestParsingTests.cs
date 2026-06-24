using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Infrastructure.Parsing;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Tests that the RSGo manifest parser deserializes the optional product-level <c>edge:</c>
/// block (camelCase keys), and that the resulting model maps to a resolved EdgeConfig.
/// </summary>
public class EdgeManifestParsingTests
{
    private static RsgoManifestParser CreateParser()
        => new(new Mock<ILogger<RsgoManifestParser>>().Object);

    [Fact]
    public async Task ParsesEdgeBlock_FromProductManifest()
    {
        const string yaml = """
            metadata:
              name: ams.project
              productVersion: "1.0.0"
            services:
              web-bff:
                image: ams/bff:1.0.0
            edge:
              enabled: true
              publicHostname: project.customer.tld
              publicPort: 443
              upstream:
                service: web-bff
                port: 8080
              network: ams-project-edge-net
              tls:
                mode: selfsigned
              maintenancePage:
                mode: default
                branding:
                  productName: "ams.project"
                  supportContact: support@customer.tld
                  locales: [de, en]
            """;

        var manifest = await CreateParser().ParseAsync(yaml);

        manifest.Edge.Should().NotBeNull();
        manifest.Edge!.Enabled.Should().BeTrue();
        manifest.Edge.PublicHostname.Should().Be("project.customer.tld");
        manifest.Edge.PublicPort.Should().Be("443");
        manifest.Edge.Upstream!.Service.Should().Be("web-bff");
        manifest.Edge.Upstream.Port.Should().Be("8080");
        manifest.Edge.Network.Should().Be("ams-project-edge-net");
        manifest.Edge.Tls!.Mode.Should().Be("selfsigned");
        manifest.Edge.MaintenancePage!.Branding!.ProductName.Should().Be("ams.project");
        manifest.Edge.MaintenancePage.Branding.Locales.Should().BeEquivalentTo(new[] { "de", "en" });

        // End-to-end: parsed model maps to a resolved EdgeConfig.
        var config = EdgeConfigMapper.Map(manifest.Edge, new Dictionary<string, string>());
        config.Should().NotBeNull();
        config!.TlsMode.Should().Be(EdgeTlsMode.SelfSigned);
        config.UpstreamService.Should().Be("web-bff");
    }

    [Fact]
    public async Task ParsesEdgeBlock_WithVariablePlaceholderPorts()
    {
        // Regression: ${VAR} placeholders in port fields must NOT break catalog-load
        // deserialization (ports are string-typed; resolved at deploy time).
        const string yaml = """
            metadata:
              name: ams.project
              productVersion: "1.0.0"
            services:
              web-bff:
                image: ams/bff:1.0.0
            edge:
              enabled: true
              publicHostname: ${AMS_PROJECT_EXTERNAL_DNS_NAME_OR_IP}
              publicPort: ${BFF_PORT}
              upstream:
                service: web-bff
                port: 8080
              network: ams-project-edge
              maintenancePage:
                mode: default
            """;

        // Must not throw — the product would otherwise disappear from the catalog.
        var manifest = await CreateParser().ParseAsync(yaml);
        manifest.Edge.Should().NotBeNull();
        manifest.Edge!.PublicPort.Should().Be("${BFF_PORT}");

        // At deploy time the placeholder resolves to the real port.
        var resolved = EdgeConfigMapper.Map(manifest.Edge, new Dictionary<string, string>
        {
            ["AMS_PROJECT_EXTERNAL_DNS_NAME_OR_IP"] = "ams.kunde.tld",
            ["BFF_PORT"] = "8443",
        });
        resolved.Should().NotBeNull();
        resolved!.PublicHostname.Should().Be("ams.kunde.tld");
        resolved.PublicPort.Should().Be(8443);

        // Unresolved placeholder falls back to the default port (still a valid config).
        var fallback = EdgeConfigMapper.Map(manifest.Edge, new Dictionary<string, string>
        {
            ["AMS_PROJECT_EXTERNAL_DNS_NAME_OR_IP"] = "ams.kunde.tld",
        });
        fallback!.PublicPort.Should().Be(443);
    }

    [Fact]
    public async Task ManifestWithoutEdgeBlock_HasNullEdge()
    {
        const string yaml = """
            metadata:
              name: legacy-product
              productVersion: "2.0.0"
            services:
              app:
                image: legacy/app:2.0.0
            """;

        var manifest = await CreateParser().ParseAsync(yaml);

        manifest.Edge.Should().BeNull("products without an edge: block stay completely unaffected");
    }
}
