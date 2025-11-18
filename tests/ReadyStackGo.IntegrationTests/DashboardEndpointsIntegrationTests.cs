using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ReadyStackGo.Api;
using ReadyStackGo.Application.Dashboard.DTOs;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Dashboard Endpoints
/// Diese Tests prüfen die Dashboard Stats API
/// </summary>
public class DashboardEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public DashboardEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var token = await TestAuthHelper.GetAdminTokenAsync(_client);
        TestAuthHelper.AddAuthToken(_client, token);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GET_DashboardStats_ReturnsStats()
    {
        // Act
        var response = await _client.GetAsync("/api/dashboard/stats");

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
        var response = await _client.GetAsync("/api/dashboard/stats");
        var stats = await response.Content.ReadFromJsonAsync<DashboardStatsDto>();

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalStacks.Should().Be(stats.DeployedStacks + stats.NotDeployedStacks);
    }

    [Fact]
    public async Task GET_DashboardStats_TotalContainersEqualsSumOfRunningAndStopped()
    {
        // Act
        var response = await _client.GetAsync("/api/dashboard/stats");
        var stats = await response.Content.ReadFromJsonAsync<DashboardStatsDto>();

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalContainers.Should().Be(stats.RunningContainers + stats.StoppedContainers);
    }

    [Fact]
    public async Task GET_DashboardStats_ReturnsConsistentData()
    {
        // Act - Get stats twice
        var response1 = await _client.GetAsync("/api/dashboard/stats");
        var stats1 = await response1.Content.ReadFromJsonAsync<DashboardStatsDto>();

        var response2 = await _client.GetAsync("/api/dashboard/stats");
        var stats2 = await response2.Content.ReadFromJsonAsync<DashboardStatsDto>();

        // Assert - Stats should be consistent
        stats1!.TotalStacks.Should().Be(stats2!.TotalStacks);
        stats1.TotalContainers.Should().Be(stats2.TotalContainers);
    }
}
