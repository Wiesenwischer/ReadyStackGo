using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Infrastructure.DataAccess.Configurations;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Round-trip and backward-compatibility coverage for persisting the client-facing MSS option
/// on <see cref="EdgeConfig"/> through <see cref="EdgeConfigJsonConverter"/> (the JSON column
/// on the product deployment).
/// </summary>
public class EdgeConfigJsonConverterMssTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new EdgeConfigJsonConverter() } };

    private static EdgeConfig RoundTrip(EdgeConfig config)
        => JsonSerializer.Deserialize<EdgeConfig>(JsonSerializer.Serialize(config, Options), Options)!;

    [Fact]
    public void RoundTrips_PmtuMode()
    {
        var original = EdgeConfig.Create("h", 443, "s", 8080, "n", "img", mssMode: EdgeMssMode.Pmtu);

        var result = RoundTrip(original);

        result.MssMode.Should().Be(EdgeMssMode.Pmtu);
        result.MssValue.Should().BeNull();
    }

    [Fact]
    public void RoundTrips_FixedModeWithValue()
    {
        var original = EdgeConfig.Create("h", 443, "s", 8080, "n", "img", mssMode: EdgeMssMode.Fixed, mssValue: 1360);

        var result = RoundTrip(original);

        result.MssMode.Should().Be(EdgeMssMode.Fixed);
        result.MssValue.Should().Be(1360);
    }

    [Fact]
    public void RoundTrips_OffMode()
    {
        var original = EdgeConfig.Create("h", 443, "s", 8080, "n", "img", mssMode: EdgeMssMode.Off);

        var result = RoundTrip(original);

        result.MssMode.Should().Be(EdgeMssMode.Off);
        result.MssValue.Should().BeNull();
    }

    [Fact]
    public void LegacyJsonWithoutMss_DefaultsToPmtu()
    {
        // An edge config persisted before this feature existed carries no mssMode/mssValue.
        const string legacyJson = """
            {
              "publicHostname": "app.test",
              "publicPort": 443,
              "upstreamService": "bff",
              "upstreamPort": 8080,
              "network": "edge-net",
              "image": "caddy:2.8.4",
              "tlsMode": "None",
              "maintenancePageMode": "Default",
              "maintenanceContainerPort": 80,
              "branding": { "locales": [] }
            }
            """;

        var result = JsonSerializer.Deserialize<EdgeConfig>(legacyJson, Options)!;

        result.MssMode.Should().Be(EdgeMssMode.Pmtu, "existing deployments become VPN-robust on upgrade without a migration");
        result.MssValue.Should().BeNull();
    }
}
