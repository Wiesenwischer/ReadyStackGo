using FluentAssertions;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.UnitTests.Infrastructure.Health;

/// <summary>
/// Tests for DockerHealthCheck model and its conversion patterns.
/// Verifies the data model used to pass Docker HEALTHCHECK to container creation.
/// </summary>
public class DockerHealthCheckMappingTests
{
    [Fact]
    public void DockerHealthCheck_AllProperties_SetCorrectly()
    {
        var hc = new DockerHealthCheck
        {
            Test = new List<string> { "CMD-SHELL", "curl -f http://localhost/ || exit 1" },
            Interval = TimeSpan.FromSeconds(30),
            Timeout = TimeSpan.FromSeconds(10),
            Retries = 3,
            StartPeriod = TimeSpan.FromSeconds(5)
        };

        hc.Test.Should().HaveCount(2);
        hc.Test[0].Should().Be("CMD-SHELL");
        hc.Interval.Should().Be(TimeSpan.FromSeconds(30));
        hc.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        hc.Retries.Should().Be(3);
        hc.StartPeriod.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DockerHealthCheck_NullOptionalFields_DefaultsCorrectly()
    {
        var hc = new DockerHealthCheck
        {
            Test = new List<string> { "CMD", "true" }
        };

        hc.Interval.Should().BeNull();
        hc.Timeout.Should().BeNull();
        hc.Retries.Should().BeNull();
        hc.StartPeriod.Should().BeNull();
    }

    [Fact]
    public void CreateContainerRequest_WithHealthCheck_IsPassedCorrectly()
    {
        var request = new CreateContainerRequest
        {
            Name = "test-container",
            Image = "redis:alpine",
            HealthCheck = new DockerHealthCheck
            {
                Test = new List<string> { "CMD", "redis-cli", "ping" },
                Interval = TimeSpan.FromSeconds(10),
                Retries = 5
            }
        };

        request.HealthCheck.Should().NotBeNull();
        request.HealthCheck!.Test.Should().Contain("redis-cli");
        request.HealthCheck.Retries.Should().Be(5);
    }

    [Fact]
    public void CreateContainerRequest_WithoutHealthCheck_IsNull()
    {
        var request = new CreateContainerRequest
        {
            Name = "test-container",
            Image = "nginx:alpine"
        };

        request.HealthCheck.Should().BeNull();
    }

    [Fact]
    public void StartPeriod_NanosecondsConversion_IsCorrect()
    {
        // Docker API uses nanoseconds for StartPeriod (long).
        // Ticks * 100 = nanoseconds (1 tick = 100ns).
        var startPeriod = TimeSpan.FromSeconds(5);
        var nanoseconds = startPeriod.Ticks * 100;

        nanoseconds.Should().Be(5_000_000_000L);
    }

    [Fact]
    public void Interval_TimeSpan_30Seconds_IsCorrect()
    {
        var interval = TimeSpan.FromSeconds(30);
        interval.TotalSeconds.Should().Be(30);
        interval.Ticks.Should().Be(300_000_000L);
    }
}
