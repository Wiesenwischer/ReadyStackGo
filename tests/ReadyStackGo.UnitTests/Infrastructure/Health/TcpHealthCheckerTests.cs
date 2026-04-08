using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Services.Health;

namespace ReadyStackGo.UnitTests.Infrastructure.Health;

/// <summary>
/// Unit tests for TcpHealthChecker.
/// Uses a local TcpListener to simulate an open port for healthy checks,
/// and unreachable ports for unhealthy checks.
/// </summary>
public class TcpHealthCheckerTests
{
    private readonly Mock<ILogger<TcpHealthChecker>> _loggerMock = new();

    private TcpHealthChecker CreateChecker() => new(_loggerMock.Object);

    [Fact]
    public async Task CheckHealth_PortOpen_ReturnsHealthy()
    {
        // Start a local TCP listener on a random port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var checker = CreateChecker();
            var config = new TcpHealthCheckConfig { Port = port, Timeout = TimeSpan.FromSeconds(5) };

            var result = await checker.CheckHealthAsync("127.0.0.1", config);

            result.IsHealthy.Should().BeTrue();
            result.ResponseTimeMs.Should().NotBeNull();
            result.ResponseTimeMs!.Value.Should().BeGreaterThanOrEqualTo(0);
            result.Error.Should().BeNull();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task CheckHealth_PortClosed_ReturnsUnhealthy()
    {
        // Use a port that is very likely not open (ephemeral range)
        var checker = CreateChecker();
        var config = new TcpHealthCheckConfig { Port = 59999, Timeout = TimeSpan.FromSeconds(2) };

        var result = await checker.CheckHealthAsync("127.0.0.1", config);

        result.IsHealthy.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CheckHealth_Timeout_ReturnsUnhealthy()
    {
        // Connect to a non-routable address to trigger timeout
        var checker = CreateChecker();
        var config = new TcpHealthCheckConfig { Port = 80, Timeout = TimeSpan.FromMilliseconds(100) };

        var result = await checker.CheckHealthAsync("192.0.2.1", config); // TEST-NET, non-routable

        result.IsHealthy.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CheckHealth_ResponseTimeMs_IsPopulated()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var checker = CreateChecker();
            var config = new TcpHealthCheckConfig { Port = port };

            var result = await checker.CheckHealthAsync("127.0.0.1", config);

            result.ResponseTimeMs.Should().NotBeNull();
            result.ResponseTimeMs.Should().BeInRange(0, 5000);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task CheckHealth_CancellationRequested_ThrowsOrReturnsUnhealthy()
    {
        var checker = CreateChecker();
        var config = new TcpHealthCheckConfig { Port = 80 };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // When the parent cancellation token is already cancelled,
        // TcpClient.ConnectAsync propagates OperationCanceledException
        var act = () => checker.CheckHealthAsync("127.0.0.1", config, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
