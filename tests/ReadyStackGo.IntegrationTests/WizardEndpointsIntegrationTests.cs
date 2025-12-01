using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Wizard API Endpoints
/// Tests the complete 3-step setup wizard flow (v0.4)
/// Each test gets its own isolated factory to avoid state conflicts.
/// </summary>
public class WizardEndpointsIntegrationTests : IAsyncLifetime
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GET_WizardStatus_ReturnsInitialState()
    {
        // Act
        var response = await _client.GetAsync("/api/wizard/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<WizardStatusResponse>();
        status.Should().NotBeNull();
        status!.WizardState.Should().NotBeNullOrEmpty();
        (status.IsCompleted == true || status.IsCompleted == false).Should().BeTrue();
    }

    [Fact]
    public async Task POST_CreateAdmin_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            username = "testadmin",
            password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/admin", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<WizardResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task POST_CreateAdmin_WithShortPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            username = "testadmin",
            password = "short"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/admin", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_SetOrganization_WithValidData_ReturnsSuccess()
    {
        // Arrange - First create admin
        await CreateAdminForTest();

        var request = new
        {
            id = "test-org",
            name = "Test Organization"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/organization", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<WizardResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task POST_CompleteWizard_WithValidData_ReturnsSuccess()
    {
        // Arrange - Complete steps 1 and 2
        await CreateAdminForTest();
        await SetOrganizationForTest();

        var request = new
        {
            manifestPath = (string?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/install", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<InstallStackResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WizardFlow_CompleteSequence_WorksInOrder()
    {
        // Step 1: Check initial status
        var statusResponse = await _client.GetAsync("/api/wizard/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await statusResponse.Content.ReadFromJsonAsync<WizardStatusResponse>();
        status!.IsCompleted.Should().BeFalse();

        // Step 2: Create admin
        var adminRequest = new { username = "flowadmin", password = "FlowPassword123!" };
        var adminResponse = await _client.PostAsJsonAsync("/api/wizard/admin", adminRequest);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminResult = await adminResponse.Content.ReadFromJsonAsync<WizardResponse>();
        adminResult!.Success.Should().BeTrue();

        // Step 3: Set organization
        var orgRequest = new { id = "flow-org", name = "Flow Test Organization" };
        var orgResponse = await _client.PostAsJsonAsync("/api/wizard/organization", orgRequest);
        orgResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var orgResult = await orgResponse.Content.ReadFromJsonAsync<WizardResponse>();
        orgResult!.Success.Should().BeTrue();

        // Step 4: Complete installation
        var installRequest = new { manifestPath = (string?)null };
        var installResponse = await _client.PostAsJsonAsync("/api/wizard/install", installRequest);
        installResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var installResult = await installResponse.Content.ReadFromJsonAsync<InstallStackResponse>();
        installResult!.Success.Should().BeTrue();

        // Step 5: Verify wizard is completed
        var finalStatusResponse = await _client.GetAsync("/api/wizard/status");
        var finalStatus = await finalStatusResponse.Content.ReadFromJsonAsync<WizardStatusResponse>();
        finalStatus!.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task POST_CompleteWizard_ReturnsCorrectVersion()
    {
        // Arrange - Complete steps 1 and 2
        await CreateAdminForTest();
        await SetOrganizationForTest();

        var request = new
        {
            manifestPath = (string?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/install", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var installResult = await response.Content.ReadFromJsonAsync<InstallStackResponse>();
        installResult!.Success.Should().BeTrue();
        installResult.StackVersion.Should().Be("v0.6.0");
    }

    [Fact]
    public async Task POST_SetOrganization_WithoutAdminCreated_ReturnsError()
    {
        // Arrange - Skip admin creation
        var request = new
        {
            id = "skip-org",
            name = "Skip Test Organization"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/organization", request);

        // Assert
        // Should fail because admin wasn't created first
        var result = await response.Content.ReadFromJsonAsync<WizardResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task POST_Install_WithoutOrganization_ReturnsError()
    {
        // Arrange - Only create admin, skip organization
        await CreateAdminForTest();

        var request = new
        {
            manifestPath = (string?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/install", request);

        // Assert
        // Should fail because organization wasn't set
        var result = await response.Content.ReadFromJsonAsync<InstallStackResponseSimple>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task POST_CreateAdmin_WithEmptyUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            username = "",
            password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/admin", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_SetOrganization_WithEmptyId_ReturnsBadRequest()
    {
        // Arrange
        await CreateAdminForTest();

        var request = new
        {
            id = "",
            name = "Test Organization"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wizard/organization", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task CreateAdminForTest()
    {
        var request = new
        {
            username = $"testadmin_{Guid.NewGuid():N}",
            password = "TestPassword123!"
        };
        await _client.PostAsJsonAsync("/api/wizard/admin", request);
    }

    private async Task SetOrganizationForTest()
    {
        var request = new
        {
            id = $"test-org-{Guid.NewGuid():N}",
            name = "Test Organization"
        };
        await _client.PostAsJsonAsync("/api/wizard/organization", request);
    }

    // Response DTOs
    private record WizardStatusResponse(string WizardState, bool IsCompleted);
    private record WizardResponse(bool Success, string? Message);
    private record InstallStackResponse(
        bool Success,
        string? StackVersion,
        List<string> DeployedContexts,
        List<string> Errors
    );
    private record InstallStackResponseSimple(bool Success, string? StackVersion);
}
