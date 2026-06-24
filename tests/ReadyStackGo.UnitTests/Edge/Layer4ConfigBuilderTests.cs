using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Application.Services.Edge;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Unit tests for the SNI passthrough router's Layer-4 config builder.
/// </summary>
public class Layer4ConfigBuilderTests
{
    private static JsonElement SniServer(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("apps").GetProperty("layer4")
            .GetProperty("servers").GetProperty("sni");

    [Fact]
    public void Build_ListensOnConfiguredPort_AndExposesAdmin()
    {
        var json = Layer4ConfigBuilder.Build(new[] { new SniRoute("a.tld", "edge-a:443") }, 443, 2019);

        JsonDocument.Parse(json).RootElement.GetProperty("admin").GetProperty("listen").GetString()
            .Should().Be("0.0.0.0:2019");
        SniServer(json).GetProperty("listen").EnumerateArray().Select(e => e.GetString()).Should().Contain(":443");
    }

    [Fact]
    public void Build_MapsEachHostnameToItsEdge_ViaSniMatchAndProxy()
    {
        var routes = new[]
        {
            new SniRoute("project-a.customer.tld", "edge-a:443"),
            new SniRoute("project-b.customer.tld", "edge-b:443")
        };

        var json = Layer4ConfigBuilder.Build(routes, 443, 2019);
        var sniRoutes = SniServer(json).GetProperty("routes");

        sniRoutes.GetArrayLength().Should().Be(2);

        var first = sniRoutes[0];
        first.GetProperty("match")[0].GetProperty("tls").GetProperty("sni").EnumerateArray()
            .Select(s => s.GetString()).Should().ContainSingle().Which.Should().Be("project-a.customer.tld");
        var handler = first.GetProperty("handle")[0];
        handler.GetProperty("handler").GetString().Should().Be("proxy");
        handler.GetProperty("upstreams")[0].GetProperty("dial").EnumerateArray()
            .Select(d => d.GetString()).Should().ContainSingle().Which.Should().Be("edge-a:443");

        // Passthrough: no TLS termination anywhere in the router config.
        json.Should().NotContain("load_pem");
        json.Should().NotContain("tls_connection_policies");
    }

    [Fact]
    public void Build_NoRoutes_ProducesEmptyButValidServer()
    {
        var json = Layer4ConfigBuilder.Build(Array.Empty<SniRoute>(), 443, 2019);
        SniServer(json).GetProperty("routes").GetArrayLength().Should().Be(0);
    }
}
