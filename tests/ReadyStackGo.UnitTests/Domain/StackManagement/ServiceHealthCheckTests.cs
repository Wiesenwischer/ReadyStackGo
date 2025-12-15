using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Domain.StackManagement;

/// <summary>
/// Unit tests for ServiceHealthCheck record.
/// </summary>
public class ServiceHealthCheckTests
{
    #region Type Property Tests

    [Fact]
    public void Type_DefaultsToDocker()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.Type.Should().Be("docker");
    }

    [Theory]
    [InlineData("docker")]
    [InlineData("http")]
    [InlineData("tcp")]
    [InlineData("none")]
    public void Type_AcceptsValidValues(string type)
    {
        // Act
        var healthCheck = new ServiceHealthCheck { Type = type };

        // Assert
        healthCheck.Type.Should().Be(type);
    }

    #endregion

    #region IsHttpHealthCheck Tests

    [Fact]
    public void IsHttpHealthCheck_WhenTypeIsHttp_ReturnsTrue()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "http" };

        // Assert
        healthCheck.IsHttpHealthCheck.Should().BeTrue();
    }

    [Fact]
    public void IsHttpHealthCheck_WhenTypeIsHttpUpperCase_ReturnsTrue()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "HTTP" };

        // Assert
        healthCheck.IsHttpHealthCheck.Should().BeTrue();
    }

    [Fact]
    public void IsHttpHealthCheck_WhenTypeIsDocker_ReturnsFalse()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "docker" };

        // Assert
        healthCheck.IsHttpHealthCheck.Should().BeFalse();
    }

    #endregion

    #region IsTcpHealthCheck Tests

    [Fact]
    public void IsTcpHealthCheck_WhenTypeIsTcp_ReturnsTrue()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "tcp" };

        // Assert
        healthCheck.IsTcpHealthCheck.Should().BeTrue();
    }

    [Fact]
    public void IsTcpHealthCheck_WhenTypeIsTcpMixedCase_ReturnsTrue()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "TCP" };

        // Assert
        healthCheck.IsTcpHealthCheck.Should().BeTrue();
    }

    [Fact]
    public void IsTcpHealthCheck_WhenTypeIsHttp_ReturnsFalse()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "http" };

        // Assert
        healthCheck.IsTcpHealthCheck.Should().BeFalse();
    }

    #endregion

    #region IsDisabled Tests

    [Fact]
    public void IsDisabled_WhenTypeIsNone_ReturnsTrue()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "none" };

        // Assert
        healthCheck.IsDisabled.Should().BeTrue();
    }

    [Fact]
    public void IsDisabled_WhenTypeIsNoneUpperCase_ReturnsTrue()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "NONE" };

        // Assert
        healthCheck.IsDisabled.Should().BeTrue();
    }

    [Fact]
    public void IsDisabled_WhenTypeIsDocker_ReturnsFalse()
    {
        // Arrange
        var healthCheck = new ServiceHealthCheck { Type = "docker" };

        // Assert
        healthCheck.IsDisabled.Should().BeFalse();
    }

    #endregion

    #region HTTP Health Check Properties Tests

    [Fact]
    public void HttpHealthCheck_WithAllProperties_StoresCorrectly()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck
        {
            Type = "http",
            Path = "/health",
            Port = 8080,
            ExpectedStatusCodes = new[] { 200, 204 },
            Https = true
        };

        // Assert
        healthCheck.Path.Should().Be("/health");
        healthCheck.Port.Should().Be(8080);
        healthCheck.ExpectedStatusCodes.Should().BeEquivalentTo(new[] { 200, 204 });
        healthCheck.Https.Should().BeTrue();
    }

    [Fact]
    public void Path_DefaultsToNull()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.Path.Should().BeNull();
    }

    [Fact]
    public void Port_DefaultsToNull()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.Port.Should().BeNull();
    }

    [Fact]
    public void Https_DefaultsToFalse()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.Https.Should().BeFalse();
    }

    [Fact]
    public void ExpectedStatusCodes_DefaultsToNull()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.ExpectedStatusCodes.Should().BeNull();
    }

    #endregion

    #region Docker HEALTHCHECK Properties Tests

    [Fact]
    public void DockerHealthCheck_WithAllProperties_StoresCorrectly()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck
        {
            Type = "docker",
            Test = new[] { "CMD", "curl", "-f", "http://localhost/health" },
            Interval = TimeSpan.FromSeconds(30),
            Timeout = TimeSpan.FromSeconds(10),
            Retries = 3,
            StartPeriod = TimeSpan.FromSeconds(5)
        };

        // Assert
        healthCheck.Test.Should().BeEquivalentTo(new[] { "CMD", "curl", "-f", "http://localhost/health" });
        healthCheck.Interval.Should().Be(TimeSpan.FromSeconds(30));
        healthCheck.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        healthCheck.Retries.Should().Be(3);
        healthCheck.StartPeriod.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Test_DefaultsToEmptyArray()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.Test.Should().BeEmpty();
    }

    [Fact]
    public void Interval_DefaultsToNull()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.Interval.Should().BeNull();
    }

    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.Timeout.Should().BeNull();
    }

    [Fact]
    public void Retries_DefaultsToNull()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.Retries.Should().BeNull();
    }

    [Fact]
    public void StartPeriod_DefaultsToNull()
    {
        // Arrange & Act
        var healthCheck = new ServiceHealthCheck();

        // Assert
        healthCheck.StartPeriod.Should().BeNull();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equality_WithSameProperties_AreEqual()
    {
        // Arrange
        var healthCheck1 = new ServiceHealthCheck
        {
            Type = "http",
            Path = "/health",
            Port = 8080
        };
        var healthCheck2 = new ServiceHealthCheck
        {
            Type = "http",
            Path = "/health",
            Port = 8080
        };

        // Assert
        healthCheck1.Should().Be(healthCheck2);
    }

    [Fact]
    public void Equality_WithDifferentProperties_AreNotEqual()
    {
        // Arrange
        var healthCheck1 = new ServiceHealthCheck { Type = "http", Port = 8080 };
        var healthCheck2 = new ServiceHealthCheck { Type = "http", Port = 9090 };

        // Assert
        healthCheck1.Should().NotBe(healthCheck2);
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    public void HttpHealthCheck_ForAspNetCoreApp_ConfiguredCorrectly()
    {
        // Arrange - Typical ASP.NET Core health check configuration
        var healthCheck = new ServiceHealthCheck
        {
            Type = "http",
            Path = "/hc",
            Port = 80,
            ExpectedStatusCodes = new[] { 200 },
            Https = false
        };

        // Assert
        healthCheck.IsHttpHealthCheck.Should().BeTrue();
        healthCheck.IsTcpHealthCheck.Should().BeFalse();
        healthCheck.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public void TcpHealthCheck_ForDatabase_ConfiguredCorrectly()
    {
        // Arrange - Database TCP connectivity check
        var healthCheck = new ServiceHealthCheck
        {
            Type = "tcp",
            Port = 5432
        };

        // Assert
        healthCheck.IsTcpHealthCheck.Should().BeTrue();
        healthCheck.IsHttpHealthCheck.Should().BeFalse();
        healthCheck.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public void DisabledHealthCheck_ForSidecarContainer_ConfiguredCorrectly()
    {
        // Arrange - Sidecar container that doesn't need health checks
        var healthCheck = new ServiceHealthCheck
        {
            Type = "none"
        };

        // Assert
        healthCheck.IsDisabled.Should().BeTrue();
        healthCheck.IsHttpHealthCheck.Should().BeFalse();
        healthCheck.IsTcpHealthCheck.Should().BeFalse();
    }

    [Fact]
    public void DockerHealthCheck_ForLegacyContainer_ConfiguredCorrectly()
    {
        // Arrange - Legacy container using Docker HEALTHCHECK
        var healthCheck = new ServiceHealthCheck
        {
            Type = "docker",
            Test = new[] { "CMD-SHELL", "curl -f http://localhost/ || exit 1" },
            Interval = TimeSpan.FromSeconds(30),
            Timeout = TimeSpan.FromSeconds(10),
            Retries = 3,
            StartPeriod = TimeSpan.FromSeconds(60)
        };

        // Assert
        healthCheck.IsHttpHealthCheck.Should().BeFalse();
        healthCheck.IsTcpHealthCheck.Should().BeFalse();
        healthCheck.IsDisabled.Should().BeFalse();
        healthCheck.Test.Should().HaveCount(2);
    }

    #endregion
}
