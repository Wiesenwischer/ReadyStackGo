using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.Application.UseCases.Dashboard;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Dashboard Endpoints
/// Diese Tests prüfen die Dashboard Stats API
/// </summary>
public class DashboardEndpointsIntegrationTests : AuthenticatedTestBase
{
    [Fact]
    public async Task GET_DashboardStats_ReturnsStats()
    {
        // Act
        var response = await Client.GetAsync("/api/dashboard/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<DashboardStatsDto>();

        stats.Should().NotBeNull();
        stats!.TotalStacks.Should().BeGreaterThanOrEqualTo(0);
        stats.DeployedStacks.Should().BeGreaterThanOrEqualTo(0);
        stats.NotDeployedStacks.Should().BeGreaterThanOrEqualTo(0);
        stats.TotalContainers.Should().BeGreaterThanOrEqualTo(0);
        stats.RunningContainers.Should().BeGreaterThanOrEqualTo(0);
        stats.StoppedContainers.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GET_DashboardStats_TotalStacksEqualsSumOfDeployedAndNotDeployed()
    {
        // Act
        var response = await Client.GetAsync("/api/dashboard/stats");
        var stats = await response.Content.ReadFromJsonAsync<DashboardStatsDto>();

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalStacks.Should().Be(stats.DeployedStacks + stats.NotDeployedStacks);
    }

    [Fact]
    public async Task GET_DashboardStats_TotalContainersEqualsSumOfRunningAndStopped()
    {
        // Act
        var response = await Client.GetAsync("/api/dashboard/stats");
        var stats = await response.Content.ReadFromJsonAsync<DashboardStatsDto>();

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalContainers.Should().Be(stats.RunningContainers + stats.StoppedContainers);
    }

    [Fact]
    public async Task GET_DashboardStats_ReturnsConsistentData()
    {
        // Act - Get stats twice
        var response1 = await Client.GetAsync("/api/dashboard/stats");
        var stats1 = await response1.Content.ReadFromJsonAsync<DashboardStatsDto>();

        var response2 = await Client.GetAsync("/api/dashboard/stats");
        var stats2 = await response2.Content.ReadFromJsonAsync<DashboardStatsDto>();

        // Assert - Stats should be consistent
        stats1!.TotalStacks.Should().Be(stats2!.TotalStacks);
        stats1.TotalContainers.Should().Be(stats2.TotalContainers);
    }
}
