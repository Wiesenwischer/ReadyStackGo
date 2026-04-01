using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for the Product Precheck API Endpoint.
/// POST /api/environments/{envId}/product-deployments/precheck
/// </summary>
public class ProductPrecheckEndpointIntegrationTests : AuthenticatedTestBase
{
    [Fact]
    public async Task POST_ProductPrecheck_WithInvalidProductId_ReturnsErrorResult()
    {
        // Arrange
        var request = new
        {
            productId = "nonexistent-product-id",
            deploymentName = "test-precheck",
            stackConfigs = new[] { new { stackId = "fake-stack", variables = new Dictionary<string, string>() } },
            sharedVariables = new Dictionary<string, string>()
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/precheck",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProductPrecheckApiResponse>();

        result.Should().NotBeNull();
        result!.CanDeploy.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
        result.Stacks.Should().NotBeEmpty();
        result.Stacks[0].Checks.Should().Contain(c => c.Rule == "Product");
    }

    [Fact]
    public async Task POST_ProductPrecheck_WithEmptyStackConfigs_ReturnsError()
    {
        // Arrange
        var request = new
        {
            productId = "some-product",
            deploymentName = "test-precheck",
            stackConfigs = Array.Empty<object>(),
            sharedVariables = new Dictionary<string, string>()
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/precheck",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProductPrecheckApiResponse>();

        result.Should().NotBeNull();
        result!.CanDeploy.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task POST_ProductPrecheck_WithoutAuth_Returns401()
    {
        // Arrange
        using var unauthClient = Factory.CreateClient();
        var request = new
        {
            productId = "test",
            deploymentName = "test-precheck",
            stackConfigs = Array.Empty<object>(),
            sharedVariables = new Dictionary<string, string>()
        };

        // Act
        var response = await unauthClient.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/precheck",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Helper DTOs for deserialization
    private class ProductPrecheckApiResponse
    {
        public bool CanDeploy { get; set; }
        public bool HasErrors { get; set; }
        public bool HasWarnings { get; set; }
        public string Summary { get; set; } = "";
        public List<StackResult> Stacks { get; set; } = [];
    }

    private class StackResult
    {
        public string StackId { get; set; } = "";
        public string StackName { get; set; } = "";
        public bool CanDeploy { get; set; }
        public bool HasErrors { get; set; }
        public bool HasWarnings { get; set; }
        public string Summary { get; set; } = "";
        public List<CheckDto> Checks { get; set; } = [];
    }

    private class CheckDto
    {
        public string Rule { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Title { get; set; } = "";
    }
}
