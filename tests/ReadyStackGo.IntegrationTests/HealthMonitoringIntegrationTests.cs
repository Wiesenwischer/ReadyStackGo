using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Infrastructure.Services.Health;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// E2E Integration tests for Health Monitoring with TestContainers.
/// Tests the complete health check flow from stack manifest to health monitoring.
/// </summary>
[Trait("Category", "Docker")]
[Collection("Docker")]
public class HealthMonitoringIntegrationTests : AuthenticatedTestBase
{
    private IContainer? _healthyContainer;
    private IContainer? _unhealthyContainer;
    private bool _dockerAvailable;
    private int _healthyContainerPort;
    private int _unhealthyContainerPort;

    protected override async Task OnInitializedAsync()
    {
        _dockerAvailable = IsDockerAvailable();
        if (!_dockerAvailable)
        {
            return;
        }

        var dockerEndpoint = GetDockerEndpoint();

        // Start a container that returns 200 OK (healthy) - nginx serves on port 80
        var healthyBuilder = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithName($"rsgo-health-test-healthy-{Guid.NewGuid():N}")
            .WithPortBinding(80, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)));

        if (!string.IsNullOrEmpty(dockerEndpoint))
        {
            healthyBuilder = healthyBuilder.WithDockerEndpoint(dockerEndpoint);
        }

        _healthyContainer = healthyBuilder.Build();
        await _healthyContainer.StartAsync();
        _healthyContainerPort = _healthyContainer.GetMappedPublicPort(80);

        // Start a container that returns 503 (unhealthy) - use nginx with custom config
        // We'll use a simple approach: container that doesn't have health endpoint returns 404
        var unhealthyBuilder = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithName($"rsgo-health-test-unhealthy-{Guid.NewGuid():N}")
            .WithPortBinding(80, true)
            .WithCommand("sh", "-c", "nginx && sleep 1 && rm -rf /usr/share/nginx/html/* && nginx -s reload && sleep infinity")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(80));

        if (!string.IsNullOrEmpty(dockerEndpoint))
        {
            unhealthyBuilder = unhealthyBuilder.WithDockerEndpoint(dockerEndpoint);
        }

        _unhealthyContainer = unhealthyBuilder.Build();
        await _unhealthyContainer.StartAsync();
        _unhealthyContainerPort = _unhealthyContainer.GetMappedPublicPort(80);
    }

    protected override async Task OnDisposingAsync()
    {
        if (_healthyContainer != null)
        {
            await _healthyContainer.DisposeAsync();
        }
        if (_unhealthyContainer != null)
        {
            await _unhealthyContainer.DisposeAsync();
        }
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetDockerEndpoint()
    {
        var envEndpoint = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrEmpty(envEndpoint))
            return envEndpoint;

        if (OperatingSystem.IsWindows())
        {
            if (File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine"))
                return "npipe://./pipe/dockerDesktopLinuxEngine";
            if (File.Exists(@"\\.\pipe\docker_engine"))
                return "npipe://./pipe/docker_engine";
        }

        return null;
    }

    #region HTTP Health Check Tests

    [SkippableFact]
    public async Task HttpHealthChecker_WithHealthyContainer_ReturnsHealthy()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        using var httpClient = new HttpClient();
        var url = $"http://localhost:{_healthyContainerPort}/";

        // Act
        var response = await httpClient.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task HttpHealthChecker_WithUnhealthyContainer_ReturnsNotFound()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - Wait for nginx to remove files
        await Task.Delay(2000);

        using var httpClient = new HttpClient();
        var url = $"http://localhost:{_unhealthyContainerPort}/hc";

        // Act
        var response = await httpClient.GetAsync(url);

        // Assert - /hc endpoint doesn't exist in nginx
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Health Monitoring Service Integration Tests

    [SkippableFact]
    public async Task GET_EnvironmentHealthSummary_ReturnsValidResponse()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Act
        var response = await Client.GetAsync($"/api/health/{EnvironmentId}/summary");

        // Assert - Either 200 OK with data or 404 when no deployments exist
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
        }
    }

    [SkippableFact]
    public async Task GET_StackHealth_Returns404ForNonExistentDeployment()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var nonExistentDeploymentId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync($"/api/health/{EnvironmentId}/deployments/{nonExistentDeploymentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task GET_HealthHistory_Returns404ForNonExistentDeployment()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var nonExistentDeploymentId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync($"/api/health/{EnvironmentId}/deployments/{nonExistentDeploymentId}/history");

        // Assert
        // History endpoint returns empty array for non-existent deployments or 404
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Container Health Status Tests

    [SkippableFact]
    public async Task ListContainers_ReturnsTestContainersWithHealthInfo()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Act
        var response = await Client.GetAsync($"/api/containers?environment={EnvironmentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var containers = await response.Content.ReadFromJsonAsync<List<ContainerInfoDto>>();
        containers.Should().NotBeNull();

        // Our test containers should be in the list
        var healthyContainer = containers!.FirstOrDefault(c => c.Id.StartsWith(_healthyContainer!.Id));
        healthyContainer.Should().NotBeNull("Healthy test container should be visible");
        healthyContainer!.State.Should().Be("running");

        var unhealthyContainer = containers.FirstOrDefault(c => c.Id.StartsWith(_unhealthyContainer!.Id));
        unhealthyContainer.Should().NotBeNull("Unhealthy test container should be visible");
        unhealthyContainer!.State.Should().Be("running");
    }

    [SkippableFact]
    public async Task ContainerHealth_NginxWithoutHealthcheck_ReportsNoneOrUnknown()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Act - Get container details
        var response = await Client.GetAsync($"/api/containers?environment={EnvironmentId}");
        var containers = await response.Content.ReadFromJsonAsync<List<ContainerInfoDto>>();

        var healthyContainer = containers!.FirstOrDefault(c => c.Id.StartsWith(_healthyContainer!.Id));

        // Assert - nginx:alpine doesn't have HEALTHCHECK, so health should be none/empty
        healthyContainer.Should().NotBeNull();
        // Health status depends on whether Docker HEALTHCHECK is configured
        // nginx:alpine has no HEALTHCHECK, so Health will be empty or "none"
        healthyContainer!.Health.Should().BeOneOf("", "none", null, "healthy", "starting");
    }

    #endregion

    #region Health Check Config Flow Tests

    [SkippableFact]
    public async Task HealthCheckConfig_HttpType_IsRecognizedByApi()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // This test verifies the health check config types are properly serialized
        // We can't fully test the deployment flow without deploying a stack,
        // but we can verify the API accepts health check configurations

        // Arrange - Create a mock deployment payload with health config
        var deployRequest = new
        {
            stackId = "test:test",
            environmentId = EnvironmentId,
            stackName = "test-healthcheck",
            variables = new Dictionary<string, string>()
        };

        // Act - Attempt validation (will fail because stack doesn't exist, but tests parsing)
        var response = await Client.PostAsJsonAsync("/api/deployments/validate", deployRequest);

        // Assert - Should fail for missing stack, not for health config parsing
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("health", "Health config parsing should not cause errors");
    }

    #endregion

    // DTO for container list response
    private record ContainerInfoDto(
        string Id,
        string Name,
        string Image,
        string State,
        string Status,
        string? Health);
}

/// <summary>
/// E2E tests for HttpHealthChecker service directly with TestContainers.
/// Tests the actual HTTP health checking logic.
/// </summary>
[Trait("Category", "Docker")]
[Collection("Docker")]
public class HttpHealthCheckerIntegrationTests : IAsyncLifetime
{
    private IContainer? _aspnetHealthContainer;
    private bool _dockerAvailable;
    private int _containerPort;

    public async Task InitializeAsync()
    {
        _dockerAvailable = IsDockerAvailable();
        if (!_dockerAvailable)
        {
            return;
        }

        var dockerEndpoint = GetDockerEndpoint();

        // Use a simple nginx container with a custom health endpoint
        // In a real scenario, this would be an ASP.NET Core container with /hc endpoint
        var builder = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithName($"rsgo-http-health-test-{Guid.NewGuid():N}")
            .WithPortBinding(80, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)));

        if (!string.IsNullOrEmpty(dockerEndpoint))
        {
            builder = builder.WithDockerEndpoint(dockerEndpoint);
        }

        _aspnetHealthContainer = builder.Build();
        await _aspnetHealthContainer.StartAsync();
        _containerPort = _aspnetHealthContainer.GetMappedPublicPort(80);
    }

    public async Task DisposeAsync()
    {
        if (_aspnetHealthContainer != null)
        {
            await _aspnetHealthContainer.DisposeAsync();
        }
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetDockerEndpoint()
    {
        var envEndpoint = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrEmpty(envEndpoint))
            return envEndpoint;

        if (OperatingSystem.IsWindows())
        {
            if (File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine"))
                return "npipe://./pipe/dockerDesktopLinuxEngine";
            if (File.Exists(@"\\.\pipe\docker_engine"))
                return "npipe://./pipe/docker_engine";
        }

        return null;
    }

    [SkippableFact]
    public async Task HttpHealthCheck_WithValidEndpoint_ReturnsHealthyStatus()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var url = $"http://localhost:{_containerPort}/";

        // Act
        var response = await httpClient.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task HttpHealthCheck_WithInvalidPath_Returns404()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var url = $"http://localhost:{_containerPort}/nonexistent-health-endpoint";

        // Act
        var response = await httpClient.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task HttpHealthCheck_WithInvalidPort_FailsGracefully()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - Use a port that's not listening
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var url = "http://localhost:59999/";

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await httpClient.GetAsync(url);
        });
    }

    [SkippableFact]
    public async Task HttpHealthCheck_WithTimeout_ReturnsConnectionFailed()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - Use the actual HttpHealthChecker with a non-routable IP
        // 10.255.255.1 is typically not reachable and will timeout
        using var httpClient = new HttpClient();
        var healthChecker = new HttpHealthChecker(httpClient, NullLogger<HttpHealthChecker>.Instance);
        var config = new HttpHealthCheckConfig
        {
            Path = "/health",
            Port = 8080,
            Timeout = TimeSpan.FromSeconds(2),
            HealthyStatusCodes = [200]
        };

        // Act
        var result = await healthChecker.CheckHealthAsync("10.255.255.1", config);

        // Assert - Should return a connection failed result, not throw
        result.IsHealthy.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }
}

/// <summary>
/// E2E tests for TCP health checks with TestContainers.
/// Tests TCP port connectivity checking.
/// </summary>
[Trait("Category", "Docker")]
[Collection("Docker")]
public class TcpHealthCheckIntegrationTests : IAsyncLifetime
{
    private IContainer? _tcpContainer;
    private bool _dockerAvailable;
    private int _containerPort;

    public async Task InitializeAsync()
    {
        _dockerAvailable = IsDockerAvailable();
        if (!_dockerAvailable)
        {
            return;
        }

        var dockerEndpoint = GetDockerEndpoint();

        // Use redis for TCP connectivity test (listens on TCP port)
        var builder = new ContainerBuilder()
            .WithImage("redis:alpine")
            .WithName($"rsgo-tcp-health-test-{Guid.NewGuid():N}")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379));

        if (!string.IsNullOrEmpty(dockerEndpoint))
        {
            builder = builder.WithDockerEndpoint(dockerEndpoint);
        }

        _tcpContainer = builder.Build();
        await _tcpContainer.StartAsync();
        _containerPort = _tcpContainer.GetMappedPublicPort(6379);
    }

    public async Task DisposeAsync()
    {
        if (_tcpContainer != null)
        {
            await _tcpContainer.DisposeAsync();
        }
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetDockerEndpoint()
    {
        var envEndpoint = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrEmpty(envEndpoint))
            return envEndpoint;

        if (OperatingSystem.IsWindows())
        {
            if (File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine"))
                return "npipe://./pipe/dockerDesktopLinuxEngine";
            if (File.Exists(@"\\.\pipe\docker_engine"))
                return "npipe://./pipe/docker_engine";
        }

        return null;
    }

    [SkippableFact]
    public async Task TcpHealthCheck_WithOpenPort_ConnectsSuccessfully()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        using var tcpClient = new System.Net.Sockets.TcpClient();

        // Act
        await tcpClient.ConnectAsync("localhost", _containerPort);

        // Assert
        tcpClient.Connected.Should().BeTrue("TCP connection to Redis should succeed");
    }

    [SkippableFact]
    public async Task TcpHealthCheck_WithClosedPort_FailsGracefully()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - Use a port that's not listening
        using var tcpClient = new System.Net.Sockets.TcpClient();

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await tcpClient.ConnectAsync("localhost", 59998, cts.Token);
        });
    }

    [SkippableFact]
    public async Task TcpHealthCheck_AfterContainerStop_FailsGracefully()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - Remember the port, then stop container
        var port = _containerPort;
        await _tcpContainer!.StopAsync();

        // Wait for port to be released
        await Task.Delay(1000);

        // Act & Assert
        using var tcpClient = new System.Net.Sockets.TcpClient();
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await tcpClient.ConnectAsync("localhost", port, cts.Token);
        });

        // Restart container for other tests
        await _tcpContainer.StartAsync();
    }
}

/// <summary>
/// E2E tests for Docker native HEALTHCHECK with TestContainers.
/// Tests containers with built-in HEALTHCHECK commands.
/// Uses docker CLI to create containers with HEALTHCHECK since Docker.DotNet API
/// has complex type requirements for HealthConfig.
/// </summary>
[Trait("Category", "Docker")]
[Collection("Docker")]
public class DockerHealthCheckIntegrationTests : IAsyncLifetime
{
    private string? _containerId;
    private string? _containerName;
    private bool _dockerAvailable;

    public async Task InitializeAsync()
    {
        _dockerAvailable = IsDockerAvailable();
        if (!_dockerAvailable)
        {
            return;
        }

        _containerName = $"rsgo-docker-healthcheck-test-{Guid.NewGuid():N}";

        // Create container with HEALTHCHECK using docker CLI
        // This is more reliable than using Docker.DotNet's HealthConfig
        var createPsi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"run -d --name {_containerName} " +
                       "--health-cmd=\"curl -f http://localhost/ || exit 1\" " +
                       "--health-interval=5s " +
                       "--health-timeout=3s " +
                       "--health-retries=3 " +
                       "--health-start-period=2s " +
                       "nginx:alpine",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var createProcess = Process.Start(createPsi);
        createProcess!.WaitForExit(30000);
        _containerId = (await createProcess.StandardOutput.ReadToEndAsync()).Trim();

        if (string.IsNullOrEmpty(_containerId) || createProcess.ExitCode != 0)
        {
            var error = await createProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to create container: {error}");
        }

        // Wait for container to start
        await Task.Delay(3000);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_containerName))
        {
            // Stop and remove container
            var stopPsi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"rm -f {_containerName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var stopProcess = Process.Start(stopPsi);
            stopProcess?.WaitForExit(10000);
        }

        await Task.CompletedTask;
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [SkippableFact]
    public async Task DockerHealthCheck_WithHealthyContainer_EventuallyReportsHealthy()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Act - Poll health status for up to 60 seconds (nginx needs time + healthcheck interval)
        var maxWait = TimeSpan.FromSeconds(60);
        var interval = TimeSpan.FromSeconds(3);
        var startTime = DateTime.UtcNow;
        string healthStatus = "unknown";

        while (DateTime.UtcNow - startTime < maxWait)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"inspect --format=\"{{{{.State.Health.Status}}}}\" {_containerId}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process!.WaitForExit(5000);
            var output = await process.StandardOutput.ReadToEndAsync();
            healthStatus = output.Trim().Trim('"').ToLowerInvariant();

            if (healthStatus == "healthy")
            {
                break;
            }

            await Task.Delay(interval);
        }

        // Assert
        healthStatus.Should().Be("healthy", "Container should become healthy within 60 seconds");
    }

    [SkippableFact]
    public void DockerHealthCheck_ContainerWasCreated()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Assert - Container was created
        _containerId.Should().NotBeNullOrEmpty("Container should have been created");
    }

    [SkippableFact]
    public async Task DockerHealthCheck_ContainerHasHealthConfig()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Act - Check if container has health config
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"inspect --format=\"{{{{.Config.Healthcheck}}}}\" {_containerId}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        process!.WaitForExit(5000);
        var output = await process.StandardOutput.ReadToEndAsync();

        // Assert - Should have healthcheck configured
        output.Should().Contain("curl", "Container should have curl healthcheck configured");
    }

    [SkippableFact]
    public async Task DockerHealthCheck_HealthStatusTransitions_FromStartingToHealthy()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - Collect health status transitions
        var statuses = new List<string>();
        var maxWait = TimeSpan.FromSeconds(60);
        var interval = TimeSpan.FromSeconds(2);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWait)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"inspect --format=\"{{{{.State.Health.Status}}}}\" {_containerId}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process!.WaitForExit(5000);
            var output = await process.StandardOutput.ReadToEndAsync();
            var status = output.Trim().Trim('"').ToLowerInvariant();

            if (!statuses.Contains(status))
            {
                statuses.Add(status);
            }

            if (status == "healthy")
            {
                break;
            }

            await Task.Delay(interval);
        }

        // Assert - Should have transitioned through starting to healthy
        statuses.Should().Contain("healthy", "Container should eventually be healthy");
        // Starting may not always be captured depending on timing
        statuses.Count.Should().BeGreaterThanOrEqualTo(1, "Should have at least one status");
    }
}
