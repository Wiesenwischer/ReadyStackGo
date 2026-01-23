using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Environment Variable API Endpoints
/// Tests environment-specific variable persistence (v0.17 feature)
/// </summary>
public class EnvironmentVariableEndpointsIntegrationTests : AuthenticatedTestBase
{
    #region Get Environment Variables

    [Fact]
    public async Task GET_GetEnvironmentVariables_ReturnsEmptyForNewEnvironment()
    {
        // Arrange - Create an environment first
        var createEnvRequest = new
        {
            name = $"Test Env {Guid.NewGuid():N}",
            socketPath = "/var/run/docker.sock"
        };

        var createEnvResponse = await Client.PostAsJsonAsync("/api/environments", createEnvRequest);
        var createEnvResult = await createEnvResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        var environmentId = createEnvResult!.Environment!.Id;

        // Act
        var response = await Client.GetAsync($"/api/environments/{environmentId}/variables");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetEnvironmentVariablesResponse>();
        result.Should().NotBeNull();
        result!.Variables.Should().NotBeNull();
        result.Variables.Should().BeEmpty();
    }

    [Fact]
    public async Task GET_GetEnvironmentVariables_WithInvalidEnvironmentId_ReturnsOK()
    {
        // Arrange
        var invalidId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync($"/api/environments/{invalidId}/variables");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetEnvironmentVariablesResponse>();
        result!.Variables.Should().BeEmpty();
    }

    [Fact]
    public async Task GET_GetEnvironmentVariables_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var environmentId = Guid.NewGuid().ToString();

        // Act
        var response = await unauthenticatedClient.GetAsync($"/api/environments/{environmentId}/variables");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Save Environment Variables

    [Fact]
    public async Task POST_SaveEnvironmentVariables_WithValidData_ReturnsSuccess()
    {
        // Arrange - Create an environment first
        var createEnvRequest = new
        {
            name = $"Test Env {Guid.NewGuid():N}",
            socketPath = "/var/run/docker.sock"
        };

        var createEnvResponse = await Client.PostAsJsonAsync("/api/environments", createEnvRequest);
        var createEnvResult = await createEnvResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        var environmentId = createEnvResult!.Environment!.Id;

        var saveRequest = new
        {
            variables = new Dictionary<string, string>
            {
                { "DATABASE_URL", "postgresql://localhost:5432/db" },
                { "API_KEY", "test-key-123" },
                { "DEBUG", "true" }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/variables", saveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SaveEnvironmentVariablesResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("saved successfully");
    }

    [Fact]
    public async Task POST_SaveEnvironmentVariables_ThenGet_ReturnsStoredVariables()
    {
        // Arrange - Create environment
        var createEnvRequest = new
        {
            name = $"Test Env {Guid.NewGuid():N}",
            socketPath = "/var/run/docker.sock"
        };

        var createEnvResponse = await Client.PostAsJsonAsync("/api/environments", createEnvRequest);
        var createEnvResult = await createEnvResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        var environmentId = createEnvResult!.Environment!.Id;

        var saveRequest = new
        {
            variables = new Dictionary<string, string>
            {
                { "APP_NAME", "ReadyStackGo" },
                { "LOG_LEVEL", "Debug" },
                { "PORT", "8080" }
            }
        };

        // Act - Save variables
        await Client.PostAsJsonAsync($"/api/environments/{environmentId}/variables", saveRequest);

        // Get variables
        var getResponse = await Client.GetAsync($"/api/environments/{environmentId}/variables");
        var getResult = await getResponse.Content.ReadFromJsonAsync<GetEnvironmentVariablesResponse>();

        // Assert
        getResult.Should().NotBeNull();
        getResult!.Variables.Should().HaveCount(3);
        getResult.Variables["APP_NAME"].Should().Be("ReadyStackGo");
        getResult.Variables["LOG_LEVEL"].Should().Be("Debug");
        getResult.Variables["PORT"].Should().Be("8080");
    }

    [Fact]
    public async Task POST_SaveEnvironmentVariables_UpdateExisting_ReturnsSuccess()
    {
        // Arrange - Create environment and save initial variables
        var createEnvRequest = new
        {
            name = $"Test Env {Guid.NewGuid():N}",
            socketPath = "/var/run/docker.sock"
        };

        var createEnvResponse = await Client.PostAsJsonAsync("/api/environments", createEnvRequest);
        var createEnvResult = await createEnvResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        var environmentId = createEnvResult!.Environment!.Id;

        var initialSaveRequest = new
        {
            variables = new Dictionary<string, string>
            {
                { "VERSION", "1.0.0" },
                { "ENVIRONMENT", "development" }
            }
        };

        await Client.PostAsJsonAsync($"/api/environments/{environmentId}/variables", initialSaveRequest);

        var updateRequest = new
        {
            variables = new Dictionary<string, string>
            {
                { "VERSION", "2.0.0" },  // Updated
                { "ENVIRONMENT", "production" },  // Updated
                { "NEW_VAR", "new-value" }  // New
            }
        };

        // Act - Update variables
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/variables", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify updates
        var getResponse = await Client.GetAsync($"/api/environments/{environmentId}/variables");
        var getResult = await getResponse.Content.ReadFromJsonAsync<GetEnvironmentVariablesResponse>();

        getResult!.Variables.Should().HaveCount(3);
        getResult.Variables["VERSION"].Should().Be("2.0.0");
        getResult.Variables["ENVIRONMENT"].Should().Be("production");
        getResult.Variables["NEW_VAR"].Should().Be("new-value");
    }

    [Fact]
    public async Task POST_SaveEnvironmentVariables_EmptyVariables_ReturnsSuccess()
    {
        // Arrange - Create environment
        var createEnvRequest = new
        {
            name = $"Test Env {Guid.NewGuid():N}",
            socketPath = "/var/run/docker.sock"
        };

        var createEnvResponse = await Client.PostAsJsonAsync("/api/environments", createEnvRequest);
        var createEnvResult = await createEnvResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        var environmentId = createEnvResult!.Environment!.Id;

        var saveRequest = new
        {
            variables = new Dictionary<string, string>()
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/variables", saveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SaveEnvironmentVariablesResponse>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task POST_SaveEnvironmentVariables_WithLongValue_ReturnsSuccess()
    {
        // Arrange - Create environment
        var createEnvRequest = new
        {
            name = $"Test Env {Guid.NewGuid():N}",
            socketPath = "/var/run/docker.sock"
        };

        var createEnvResponse = await Client.PostAsJsonAsync("/api/environments", createEnvRequest);
        var createEnvResult = await createEnvResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        var environmentId = createEnvResult!.Environment!.Id;

        var longValue = new string('x', 5000);
        var saveRequest = new
        {
            variables = new Dictionary<string, string>
            {
                { "LONG_CONFIG", longValue }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/variables", saveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify stored correctly
        var getResponse = await Client.GetAsync($"/api/environments/{environmentId}/variables");
        var getResult = await getResponse.Content.ReadFromJsonAsync<GetEnvironmentVariablesResponse>();

        getResult!.Variables["LONG_CONFIG"].Should().Be(longValue);
    }

    [Fact]
    public async Task POST_SaveEnvironmentVariables_WithSpecialCharacters_ReturnsSuccess()
    {
        // Arrange - Create environment
        var createEnvRequest = new
        {
            name = $"Test Env {Guid.NewGuid():N}",
            socketPath = "/var/run/docker.sock"
        };

        var createEnvResponse = await Client.PostAsJsonAsync("/api/environments", createEnvRequest);
        var createEnvResult = await createEnvResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        var environmentId = createEnvResult!.Environment!.Id;

        var saveRequest = new
        {
            variables = new Dictionary<string, string>
            {
                { "APP_CONFIG__DATABASE__HOST", "localhost" },
                { "SPECIAL_CHARS", "!@#$%^&*(){}[]|\\:;\"'<>,.?/" }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/variables", saveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify
        var getResponse = await Client.GetAsync($"/api/environments/{environmentId}/variables");
        var getResult = await getResponse.Content.ReadFromJsonAsync<GetEnvironmentVariablesResponse>();

        getResult!.Variables["APP_CONFIG__DATABASE__HOST"].Should().Be("localhost");
        getResult.Variables["SPECIAL_CHARS"].Should().Be("!@#$%^&*(){}[]|\\:;\"'<>,.?/");
    }

    [Fact]
    public async Task POST_SaveEnvironmentVariables_MultipleEnvironments_IsolatesVariables()
    {
        // Arrange - Create two environments
        var createEnv1Request = new { name = $"Env1 {Guid.NewGuid():N}", socketPath = "/var/run/docker.sock" };
        var createEnv2Request = new { name = $"Env2 {Guid.NewGuid():N}", socketPath = "/var/run/docker.sock" };

        var env1Response = await Client.PostAsJsonAsync("/api/environments", createEnv1Request);
        var env2Response = await Client.PostAsJsonAsync("/api/environments", createEnv2Request);

        var env1Result = await env1Response.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        var env2Result = await env2Response.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();

        var env1Id = env1Result!.Environment!.Id;
        var env2Id = env2Result!.Environment!.Id;

        // Act - Save different variables to each environment
        await Client.PostAsJsonAsync($"/api/environments/{env1Id}/variables", new
        {
            variables = new Dictionary<string, string> { { "ENV", "env1" }, { "VALUE", "value1" } }
        });

        await Client.PostAsJsonAsync($"/api/environments/{env2Id}/variables", new
        {
            variables = new Dictionary<string, string> { { "ENV", "env2" }, { "VALUE", "value2" } }
        });

        // Assert - Verify isolation
        var env1GetResponse = await Client.GetAsync($"/api/environments/{env1Id}/variables");
        var env1GetResult = await env1GetResponse.Content.ReadFromJsonAsync<GetEnvironmentVariablesResponse>();

        var env2GetResponse = await Client.GetAsync($"/api/environments/{env2Id}/variables");
        var env2GetResult = await env2GetResponse.Content.ReadFromJsonAsync<GetEnvironmentVariablesResponse>();

        env1GetResult!.Variables["ENV"].Should().Be("env1");
        env1GetResult.Variables["VALUE"].Should().Be("value1");

        env2GetResult!.Variables["ENV"].Should().Be("env2");
        env2GetResult.Variables["VALUE"].Should().Be("value2");
    }

    [Fact]
    public async Task POST_SaveEnvironmentVariables_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var environmentId = Guid.NewGuid().ToString();

        var saveRequest = new
        {
            variables = new Dictionary<string, string> { { "KEY", "value" } }
        };

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync($"/api/environments/{environmentId}/variables", saveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Response DTOs

    private record CreateEnvironmentResponse(bool Success, string? Message, EnvironmentDto? Environment);
    private record EnvironmentDto(string Id, string Name, string Type, string ConnectionString, bool IsDefault, DateTime CreatedAt);
    private record GetEnvironmentVariablesResponse(Dictionary<string, string> Variables);
    private record SaveEnvironmentVariablesResponse(bool Success, string? Message);

    #endregion
}
