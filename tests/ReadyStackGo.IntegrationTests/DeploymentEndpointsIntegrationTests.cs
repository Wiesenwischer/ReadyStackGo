using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Deployment API Endpoints
/// Tests Docker Compose parsing and stack deployment (v0.4 feature)
/// </summary>
public class DeploymentEndpointsIntegrationTests : AuthenticatedTestBase
{
    #region Test Data

    // RSGo manifest format - simple stack with one service
    private const string SimpleComposeYaml = @"
metadata:
  name: Simple Web Stack
  description: A simple web server
  productVersion: 1.0.0

services:
  web:
    image: nginx:latest
    ports:
      - '80:80'
";

    // RSGo manifest format - stack with variables
    private const string ComposeWithVariables = @"
metadata:
  name: Database Stack
  description: PostgreSQL database with variables
  productVersion: 1.0.0

variables:
  POSTGRES_VERSION:
    description: PostgreSQL version
    default: '15'
  DB_USER:
    description: Database user
    required: true
  DB_PASSWORD:
    description: Database password
    required: true
  DB_NAME:
    description: Database name
    default: mydb
  DB_PORT:
    description: Database port
    default: '5432'

services:
  db:
    image: postgres:${POSTGRES_VERSION}
    environment:
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: ${DB_NAME}
    ports:
      - '${DB_PORT}:5432'
";

    // RSGo manifest format - multiple services
    private const string MultiServiceCompose = @"
metadata:
  name: Multi-Service Stack
  description: Stack with multiple services
  productVersion: 1.0.0

services:
  frontend:
    image: nginx:latest
    ports:
      - '80:80'
  backend:
    image: node:18
    ports:
      - '3000:3000'
  database:
    image: postgres:15
    environment:
      POSTGRES_PASSWORD: secret
  cache:
    image: redis:7
";

    // RSGo manifest format - with networks and volumes
    private const string ComposeWithNetworksAndVolumes = @"
metadata:
  name: App Stack
  description: Application with networks and volumes
  productVersion: 1.0.0

services:
  app:
    image: myapp:latest
    networks:
      - frontend
      - backend
    volumes:
      - app-data:/data

networks:
  frontend:
    driver: bridge
  backend:
    driver: bridge

volumes:
  app-data:
    driver: local
";

    #endregion

    #region Parse Compose - Basic

    [Fact]
    public async Task POST_ParseCompose_WithValidSimpleYaml_ReturnsSuccess()
    {
        // Arrange
        var request = new { yamlContent = SimpleComposeYaml };

        // Act
        var response = await Client.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ParseComposeResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Services.Should().ContainSingle().Which.Should().Be("web");
        result.Variables.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task POST_ParseCompose_WithEmptyContent_ReturnsError()
    {
        // Arrange
        var request = new { yamlContent = "" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        // The API returns an error - check either the status code or response content
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<ParseComposeResponseSimple>();
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
        }
        else
        {
            // FastEndpoints returns 400 with validation errors
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task POST_ParseCompose_WithInvalidYaml_ReturnsError()
    {
        // Arrange
        var request = new { yamlContent = "invalid: : : yaml" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        // The API returns an error - check either the status code or response content
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<ParseComposeResponseSimple>();
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
        }
        else
        {
            // FastEndpoints returns 400 with validation errors
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task POST_ParseCompose_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var request = new { yamlContent = SimpleComposeYaml };

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Parse Compose - Variable Detection

    [Fact]
    public async Task POST_ParseCompose_WithVariables_DetectsAllVariables()
    {
        // Arrange
        var request = new { yamlContent = ComposeWithVariables };

        // Act
        var response = await Client.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ParseComposeResponse>();
        result!.Success.Should().BeTrue();
        result.Services.Should().ContainSingle().Which.Should().Be("db");
        result.Variables.Should().HaveCount(5);
    }

    [Fact]
    public async Task POST_ParseCompose_WithVariables_IdentifiesRequiredVariables()
    {
        // Arrange
        var request = new { yamlContent = ComposeWithVariables };

        // Act
        var response = await Client.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ParseComposeResponse>();
        result!.Success.Should().BeTrue();

        // Required variables (no default value)
        result.Variables.Should().Contain(v => v.Name == "DB_USER" && v.IsRequired);
        result.Variables.Should().Contain(v => v.Name == "DB_PASSWORD" && v.IsRequired);
    }

    [Fact]
    public async Task POST_ParseCompose_WithVariables_ParsesDefaultValues()
    {
        // Arrange
        var request = new { yamlContent = ComposeWithVariables };

        // Act
        var response = await Client.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ParseComposeResponse>();
        result!.Success.Should().BeTrue();

        // Optional variables (with default values)
        result.Variables.Should().Contain(v =>
            v.Name == "POSTGRES_VERSION" && !v.IsRequired && v.DefaultValue == "15");
        result.Variables.Should().Contain(v =>
            v.Name == "DB_NAME" && !v.IsRequired && v.DefaultValue == "mydb");
        result.Variables.Should().Contain(v =>
            v.Name == "DB_PORT" && !v.IsRequired && v.DefaultValue == "5432");
    }

    #endregion

    #region Parse Compose - Multiple Services

    [Fact]
    public async Task POST_ParseCompose_WithMultipleServices_DetectsAllServices()
    {
        // Arrange
        var request = new { yamlContent = MultiServiceCompose };

        // Act
        var response = await Client.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ParseComposeResponse>();
        result!.Success.Should().BeTrue();
        result.Services.Should().HaveCount(4);
        result.Services.Should().Contain("frontend");
        result.Services.Should().Contain("backend");
        result.Services.Should().Contain("database");
        result.Services.Should().Contain("cache");
    }

    [Fact]
    public async Task POST_ParseCompose_WithNetworksAndVolumes_ParsesSuccessfully()
    {
        // Arrange
        var request = new { yamlContent = ComposeWithNetworksAndVolumes };

        // Act
        var response = await Client.PostAsJsonAsync("/api/deployments/parse", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ParseComposeResponse>();
        result!.Success.Should().BeTrue();
        result.Services.Should().ContainSingle().Which.Should().Be("app");
    }

    #endregion

    #region Deploy Compose

    [Fact]
    public async Task POST_DeployCompose_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("Deploy Test Environment");

        var request = new
        {
            stackName = "test-stack",
            yamlContent = SimpleComposeYaml,
            variables = new Dictionary<string, string>()
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/deployments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DeployComposeResponse>();
        result.Should().NotBeNull();
        // Actual deployment might fail without Docker, but endpoint should respond correctly
    }

    [Fact]
    public async Task POST_DeployCompose_WithEmptyStackName_ProcessesRequest()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("Empty Stack Name Test");

        var request = new
        {
            stackName = "",
            yamlContent = SimpleComposeYaml,
            variables = new Dictionary<string, string>()
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/deployments", request);

        // Assert
        // The API accepts the request - actual validation happens at deployment time
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DeployComposeResponseSimple>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_DeployCompose_WithInvalidEnvironmentId_ReturnsError()
    {
        // Arrange
        var request = new
        {
            stackName = "test-stack",
            yamlContent = SimpleComposeYaml,
            variables = new Dictionary<string, string>()
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/environments/invalid-environment-id/deployments", request);

        // Assert
        // v0.6: The API may return either:
        // - 400 Bad Request with error details (if validation fails early)
        // - 200 OK with Success=false (if the service handles the error)
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<DeployComposeResponseSimple>();
            result!.Success.Should().BeFalse("Invalid environment ID should result in failure");
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task POST_DeployCompose_WithVariables_PassesVariables()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("Variables Deploy Test");

        var request = new
        {
            stackName = "db-stack",
            yamlContent = ComposeWithVariables,
            variables = new Dictionary<string, string>
            {
                ["DB_USER"] = "testuser",
                ["DB_PASSWORD"] = "testpassword"
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/deployments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DeployComposeResponse>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_DeployCompose_ReturnsDeploymentSessionId()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("SessionId Test Environment");

        var request = new
        {
            stackName = "session-test-stack",
            yamlContent = SimpleComposeYaml,
            variables = new Dictionary<string, string>()
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/deployments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DeployComposeResponseSimple>();
        result.Should().NotBeNull();

        // DeploymentSessionId should be set for SignalR progress tracking
        result!.DeploymentSessionId.Should().NotBeNullOrEmpty(
            "Response should include a DeploymentSessionId for SignalR progress tracking");
        result.DeploymentSessionId.Should().StartWith("session-test-stack-",
            "DeploymentSessionId should be prefixed with the stack name");
    }

    #endregion

    #region List Deployments

    [Fact]
    public async Task GET_ListDeployments_ReturnsSuccess()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("List Deployments Test");

        // Act
        var response = await Client.GetAsync($"/api/environments/{environmentId}/deployments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ListDeploymentsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Deployments.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_ListDeployments_WithInvalidEnvironmentId_ReturnsEmptyList()
    {
        // Act
        var response = await Client.GetAsync("/api/environments/invalid-environment-id/deployments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ListDeploymentsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Deployments.Should().BeEmpty();
    }

    #endregion

    #region Get Deployment

    [Fact]
    public async Task GET_GetDeployment_WithInvalidStackName_ReturnsNotFound()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("Get Deployment Test");

        // Act
        var response = await Client.GetAsync($"/api/environments/{environmentId}/deployments/nonexistent-stack");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Remove Deployment

    [Fact]
    public async Task DELETE_RemoveDeployment_WithNonexistentStack_ReturnsErrorOrSuccess()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("Remove Deployment Test");

        // Act
        var response = await Client.DeleteAsync($"/api/environments/{environmentId}/deployments/nonexistent-stack");

        // Assert
        // DELETE is idempotent per RFC 7231 - removing a non-existent stack should succeed
        // However, if Docker is not accessible in the test environment, it returns 400.
        // Both are acceptable behaviors depending on the test environment:
        // - 200 OK: Docker is accessible, no containers found, success
        // - 400 Bad Request: Docker is not accessible, operation cannot be performed
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Complete Flow

    [Fact]
    public async Task DeploymentFlow_ParseAndDeploy_WorksInSequence()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("Flow Deploy Test");

        // Step 1: Parse compose file
        var parseRequest = new { yamlContent = ComposeWithVariables };
        var parseResponse = await Client.PostAsJsonAsync("/api/deployments/parse", parseRequest);
        parseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var parsed = await parseResponse.Content.ReadFromJsonAsync<ParseComposeResponse>();
        parsed!.Success.Should().BeTrue();
        parsed.Services.Should().Contain("db");
        parsed.Variables.Should().HaveCount(5);

        // Step 2: Deploy with required variables
        var deployRequest = new
        {
            stackName = "flow-test-stack",
            yamlContent = ComposeWithVariables,
            variables = new Dictionary<string, string>
            {
                ["DB_USER"] = "flowuser",
                ["DB_PASSWORD"] = "flowpassword"
            }
        };
        var deployResponse = await Client.PostAsJsonAsync($"/api/environments/{environmentId}/deployments", deployRequest);
        deployResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: List deployments
        var listResponse = await Client.GetAsync($"/api/environments/{environmentId}/deployments");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<ListDeploymentsResponse>();
        list!.Success.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private async Task<string> CreateTestEnvironment(string name)
    {
        var request = new
        {
            name = name,
            socketPath = "/var/run/docker.sock"
        };

        var response = await Client.PostAsJsonAsync("/api/environments", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EnvironmentResponse>();
        if (result?.Environment?.Id == null)
        {
            throw new InvalidOperationException($"Failed to create test environment: {result?.Message}");
        }

        return result.Environment.Id;
    }

    #endregion

    #region Response DTOs

    // Full DTOs for successful responses
    private record VariableInfo(string Name, string? DefaultValue, bool IsRequired, List<string>? UsedInServices);
    private record ParseComposeResponse(
        bool Success,
        string? Message,
        List<string> Services,
        List<VariableInfo> Variables,
        List<string> Warnings,
        List<string> Errors
    );
    private record DeployComposeResponse(
        bool Success,
        string? Message,
        string? DeploymentId,
        string? StackName,
        List<DeployedServiceInfo>? Services,
        List<string> Errors,
        string? DeploymentSessionId
    );
    private record DeployedServiceInfo(string ServiceName, string? ContainerId, string? Status, List<string>? Ports);
    private record DeploymentSummary(string DeploymentId, string StackName, DateTime DeployedAt, int ServiceCount, string? Status);
    private record ListDeploymentsResponse(bool Success, List<DeploymentSummary> Deployments);
    private record GetDeploymentResponse(bool Success, string? Message);
    private record EnvironmentDto(string Id, string Name, string Type, string ConnectionString, bool IsDefault);
    private record EnvironmentResponse(bool Success, string? Message, EnvironmentDto? Environment);

    // Simplified DTOs for error responses (to avoid JSON deserialization issues)
    private record ParseComposeResponseSimple(bool Success, string? Message);
    private record DeployComposeResponseSimple(bool Success, string? Message, string? DeploymentSessionId);

    #endregion
}
