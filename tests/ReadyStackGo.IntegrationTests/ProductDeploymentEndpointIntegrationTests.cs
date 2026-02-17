using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for ProductDeployment API Endpoints.
/// Tests endpoint routing, authentication, and basic serialization.
/// Full orchestration tests require complex setup and are covered in Feature 11.
/// </summary>
public class ProductDeploymentEndpointIntegrationTests : AuthenticatedTestBase
{
    #region List Product Deployments

    [Fact]
    public async Task ListProductDeployments_ReturnsEmptyList()
    {
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListProductDeploymentsApiResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ProductDeployments.Should().BeEmpty();
    }

    [Fact]
    public async Task ListProductDeployments_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthClient = CreateUnauthenticatedClient();

        var response = await unauthClient.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Product Deployment

    [Fact]
    public async Task GetProductDeployment_WithNonExistentId_ReturnsError()
    {
        var fakeId = Guid.NewGuid().ToString();

        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/{fakeId}");

        // FastEndpoints returns 400 for ThrowError
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductDeployment_WithInvalidId_ReturnsError()
    {
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/invalid-id");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductDeployment_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthClient = CreateUnauthenticatedClient();

        var response = await unauthClient.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Product Deployment By Product

    [Fact]
    public async Task GetProductDeploymentByProduct_WithNoDeployment_ReturnsError()
    {
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/by-product/nonexistent:product");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductDeploymentByProduct_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthClient = CreateUnauthenticatedClient();

        var response = await unauthClient.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/by-product/test:product");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Deploy Product

    [Fact]
    public async Task DeployProduct_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthClient = CreateUnauthenticatedClient();

        var request = new
        {
            productId = "test:product:1.0.0",
            stackConfigs = Array.Empty<object>(),
            sharedVariables = new Dictionary<string, string>()
        };

        var response = await unauthClient.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/product-deployments", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeployProduct_WithNonExistentProduct_ReturnsError()
    {
        var request = new
        {
            productId = "nonexistent:product:99.0.0",
            stackConfigs = new[]
            {
                new
                {
                    stackId = "nonexistent:product:stack:99.0.0",
                    deploymentStackName = "test-stack",
                    variables = new Dictionary<string, string>()
                }
            },
            sharedVariables = new Dictionary<string, string>()
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/product-deployments", request);

        // Should fail because product doesn't exist in catalog
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Upgrade Product

    [Fact]
    public async Task UpgradeProduct_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthClient = CreateUnauthenticatedClient();
        var fakeId = Guid.NewGuid().ToString();

        var request = new
        {
            targetProductId = "test:product:2.0.0",
            stackConfigs = Array.Empty<object>(),
            sharedVariables = new Dictionary<string, string>()
        };

        var response = await unauthClient.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/{fakeId}/upgrade", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpgradeProduct_WithNonExistentDeployment_ReturnsError()
    {
        var fakeId = Guid.NewGuid().ToString();

        var request = new
        {
            targetProductId = "test:product:2.0.0",
            stackConfigs = new[]
            {
                new
                {
                    stackId = "test:product:stack:2.0.0",
                    deploymentStackName = "test-stack",
                    variables = new Dictionary<string, string>()
                }
            },
            sharedVariables = new Dictionary<string, string>()
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/{fakeId}/upgrade", request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    #endregion

    #region Check Product Upgrade

    [Fact]
    public async Task CheckProductUpgrade_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthClient = CreateUnauthenticatedClient();
        var fakeId = Guid.NewGuid().ToString();

        var response = await unauthClient.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/{fakeId}/upgrade/check");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CheckProductUpgrade_WithNonExistentDeployment_ReturnsError()
    {
        var fakeId = Guid.NewGuid().ToString();

        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/{fakeId}/upgrade/check");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckProductUpgrade_WithInvalidId_ReturnsError()
    {
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/product-deployments/not-a-guid/upgrade/check");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    #endregion

    #region Response DTOs

    private record ListProductDeploymentsApiResponse(
        bool Success,
        List<ProductDeploymentSummaryApiResponse> ProductDeployments);

    private record ProductDeploymentSummaryApiResponse(
        string ProductDeploymentId,
        string ProductName,
        string ProductDisplayName,
        string ProductVersion,
        string Status,
        int TotalStacks,
        int CompletedStacks,
        int FailedStacks);

    #endregion
}
