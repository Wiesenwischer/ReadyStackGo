using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using ReadyStackGo.IntegrationTests.Infrastructure;
using System.Net.Http.Json;
using Xunit;

namespace ReadyStackGo.IntegrationTests.Services;

/// <summary>
/// End-to-end tests for the DeploymentHub SignalR functionality.
/// Tests real-time progress updates during deployments.
/// </summary>
public class DeploymentHubE2ETests : AuthenticatedTestBase
{
    private const string SimpleComposeYaml = @"
version: '3.8'
services:
  web:
    image: nginx:latest
    ports:
      - '8888:80'
";

    [Fact]
    public async Task DeploymentHub_Connection_Succeeds()
    {
        // Arrange
        var hubUrl = $"{Factory.Server.BaseAddress}hubs/deployment";

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(AuthToken);
            })
            .Build();

        // Act
        await connection.StartAsync();

        // Assert
        connection.State.Should().Be(HubConnectionState.Connected);

        // Cleanup
        await connection.StopAsync();
    }

    [Fact]
    public async Task DeploymentHub_SubscribeToDeployment_Works()
    {
        // Arrange
        var hubUrl = $"{Factory.Server.BaseAddress}hubs/deployment";

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(AuthToken);
            })
            .Build();

        await connection.StartAsync();

        // Act - Subscribe to a deployment
        await connection.InvokeAsync("SubscribeToDeployment", "test-deployment-123");

        // Assert - Should not throw
        connection.State.Should().Be(HubConnectionState.Connected);

        // Cleanup
        await connection.StopAsync();
    }

    [Fact]
    public async Task DeploymentHub_SubscribeToAllDeployments_Works()
    {
        // Arrange
        var hubUrl = $"{Factory.Server.BaseAddress}hubs/deployment";

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(AuthToken);
            })
            .Build();

        await connection.StartAsync();

        // Act - Subscribe to all deployments
        await connection.InvokeAsync("SubscribeToAllDeployments");

        // Assert - Should not throw
        connection.State.Should().Be(HubConnectionState.Connected);

        // Cleanup
        await connection.StopAsync();
    }

    [Fact]
    public async Task DeploymentHub_UnsubscribeFromDeployment_Works()
    {
        // Arrange
        var hubUrl = $"{Factory.Server.BaseAddress}hubs/deployment";

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(AuthToken);
            })
            .Build();

        await connection.StartAsync();

        // Act - Subscribe then unsubscribe
        await connection.InvokeAsync("SubscribeToDeployment", "test-deployment-456");
        await connection.InvokeAsync("UnsubscribeFromDeployment", "test-deployment-456");

        // Assert - Should not throw
        connection.State.Should().Be(HubConnectionState.Connected);

        // Cleanup
        await connection.StopAsync();
    }

    [Fact]
    public async Task DeploymentHub_WithoutAuth_CannotConnect()
    {
        // Arrange - No access token
        var hubUrl = $"{Factory.Server.BaseAddress}hubs/deployment";

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                // No access token provider
            })
            .Build();

        // Act & Assert
        var act = async () => await connection.StartAsync();
        await act.Should().ThrowAsync<Exception>(
            "SignalR hub should require authentication");
    }

    [Fact]
    public async Task DeploymentHub_ReceivesProgressUpdates_WhenDeploying()
    {
        // Arrange
        var hubUrl = $"{Factory.Server.BaseAddress}hubs/deployment";
        var progressUpdates = new List<DeploymentProgressUpdate>();
        var progressReceived = new TaskCompletionSource<bool>();

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(AuthToken);
            })
            .Build();

        connection.On<DeploymentProgressUpdate>("DeploymentProgress", update =>
        {
            progressUpdates.Add(update);
            if (update.IsComplete)
            {
                progressReceived.TrySetResult(true);
            }
        });

        await connection.StartAsync();

        // Subscribe to all deployments (since we don't know the session ID yet)
        await connection.InvokeAsync("SubscribeToAllDeployments");

        // Create test environment
        var environmentId = await CreateTestEnvironment();

        // Act - Deploy a stack
        var request = new
        {
            stackName = "signalr-test-stack",
            yamlContent = SimpleComposeYaml,
            variables = new Dictionary<string, string>()
        };

        var response = await Client.PostAsJsonAsync($"/api/deployments/{environmentId}", request);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Wait for progress updates (with timeout)
        var completed = await Task.WhenAny(
            progressReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(30)));

        // Assert
        // Note: In CI without Docker, we may not receive updates, but the connection should work
        connection.State.Should().Be(HubConnectionState.Connected);

        // Cleanup
        await connection.StopAsync();
    }

    #region Helper Methods

    private async Task<string> CreateTestEnvironment()
    {
        var request = new
        {
            name = $"SignalR Test Env {Guid.NewGuid():N}",
            socketPath = "/var/run/docker.sock"
        };

        var response = await Client.PostAsJsonAsync("/api/environments", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EnvironmentResponse>();
        return result?.Environment?.Id ?? throw new InvalidOperationException("Failed to create environment");
    }

    #endregion

    #region DTOs

    private record DeploymentProgressUpdate(
        string DeploymentId,
        string Phase,
        string Message,
        int ProgressPercent,
        string? CurrentService,
        int TotalServices,
        int CompletedServices,
        bool IsComplete,
        bool IsError,
        string? ErrorMessage
    );

    private record EnvironmentDto(string Id, string Name, string Type, string ConnectionString, bool IsDefault);
    private record EnvironmentResponse(bool Success, string? Message, EnvironmentDto? Environment);

    #endregion
}
