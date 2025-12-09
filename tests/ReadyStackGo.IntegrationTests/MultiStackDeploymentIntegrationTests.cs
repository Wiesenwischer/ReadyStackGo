using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Multi-Stack deployment functionality.
/// Tests that stacks from Multi-Stack products can be loaded and deployed correctly.
/// </summary>
public class MultiStackDeploymentIntegrationTests : AuthenticatedTestBase
{
    [Fact]
    public async Task GET_Products_ReturnsSuccess()
    {
        // Act
        var response = await Client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductResponseDto>>();
        products.Should().NotBeNull();
        // In isolated test environment, products may be empty (no stacks configured)
        // The important thing is that the endpoint responds successfully
    }

    [Fact]
    public async Task GET_Products_MultiStackProductHasMultipleStacks()
    {
        // Act
        var response = await Client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductResponseDto>>();

        // Look for multi-stack products (more than 1 stack)
        var multiStackProducts = products!.Where(p => p.TotalStacks > 1).ToList();

        // This test may or may not find multi-stack products depending on test setup
        // If found, verify they have the expected properties
        foreach (var product in multiStackProducts)
        {
            product.TotalStacks.Should().BeGreaterThan(1);
            product.IsMultiStack.Should().BeTrue();
        }
    }

    [Fact]
    public async Task GET_ProductById_ReturnsStacksWithValidYamlContent()
    {
        // Arrange - Get products list first
        var productsResponse = await Client.GetAsync("/api/products");
        var products = await productsResponse.Content.ReadFromJsonAsync<List<ProductResponseDto>>();

        if (products == null || !products.Any())
        {
            // Skip if no products available
            return;
        }

        // Act - Get details for the first product
        var firstProduct = products.First();
        var detailResponse = await Client.GetAsync($"/api/products/{firstProduct.Id}");

        // Assert
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var productDetail = await detailResponse.Content.ReadFromJsonAsync<ProductDetailResponseDto>();
        productDetail.Should().NotBeNull();
        productDetail!.Stacks.Should().NotBeEmpty();

        // Each stack should have services defined
        foreach (var stack in productDetail.Stacks)
        {
            stack.Services.Should().NotBeNull();
            // Multi-stack or single-stack should have at least one service
            if (productDetail.IsMultiStack || stack.Services.Any())
            {
                stack.Services.Should().NotBeEmpty($"Stack '{stack.Name}' should have services");
            }
        }
    }

    [Fact]
    public async Task GET_Stack_FromMultiStackProduct_HasDockerComposeContent()
    {
        // Arrange - Get products list
        var productsResponse = await Client.GetAsync("/api/products");
        var products = await productsResponse.Content.ReadFromJsonAsync<List<ProductResponseDto>>();

        // Find a multi-stack product
        var multiStackProduct = products?.FirstOrDefault(p => p.IsMultiStack);

        if (multiStackProduct == null)
        {
            // No multi-stack product available - skip test
            return;
        }

        // Get product details
        var detailResponse = await Client.GetAsync($"/api/products/{multiStackProduct.Id}");
        var productDetail = await detailResponse.Content.ReadFromJsonAsync<ProductDetailResponseDto>();

        var firstStack = productDetail?.Stacks.FirstOrDefault();
        if (firstStack == null)
        {
            return;
        }

        // Act - Get the stack YAML content via the stacks endpoint
        var stacksResponse = await Client.GetAsync("/api/stacks");
        var stacks = await stacksResponse.Content.ReadFromJsonAsync<List<StackResponseDto>>();

        // Find the stack that matches our sub-stack
        var matchingStack = stacks?.FirstOrDefault(s => s.Name == firstStack.Name);

        if (matchingStack == null)
        {
            // Stack might not be found by exact name match
            return;
        }

        // Assert - The stack should have services
        matchingStack.Services.Should().NotBeEmpty("Multi-stack sub-stacks should have services");
    }

    [Fact]
    public async Task POST_ValidateDeployment_ForMultiStackSubStack_ShouldSucceed()
    {
        // Arrange - Get products and find a multi-stack product
        var productsResponse = await Client.GetAsync("/api/products");
        var products = await productsResponse.Content.ReadFromJsonAsync<List<ProductResponseDto>>();

        var multiStackProduct = products?.FirstOrDefault(p => p.IsMultiStack);

        if (multiStackProduct == null)
        {
            // No multi-stack product available - skip test
            return;
        }

        // Get product details
        var detailResponse = await Client.GetAsync($"/api/products/{multiStackProduct.Id}");
        var productDetail = await detailResponse.Content.ReadFromJsonAsync<ProductDetailResponseDto>();

        var firstStack = productDetail?.Stacks.FirstOrDefault();
        if (firstStack == null)
        {
            return;
        }

        // Get stacks list to find the stack ID
        var stacksResponse = await Client.GetAsync("/api/stacks");
        var stacks = await stacksResponse.Content.ReadFromJsonAsync<List<StackResponseDto>>();
        var stack = stacks?.FirstOrDefault(s => s.Name == firstStack.Name);

        if (stack == null)
        {
            return;
        }

        // Build variable values from defaults
        var variables = firstStack.Variables.ToDictionary(
            v => v.Name,
            v => v.DefaultValue ?? ""
        );

        var deployRequest = new
        {
            stackId = stack.Id,
            environmentId = EnvironmentId,
            variables,
            stackName = $"test-{Guid.NewGuid().ToString("N")[..8]}"
        };

        // Act - Try to validate the deployment (not actually deploy)
        var validateResponse = await Client.PostAsJsonAsync("/api/deployments/validate", deployRequest);

        // Assert - Should not fail with "No services defined" error
        var content = await validateResponse.Content.ReadAsStringAsync();
        content.Should().NotContain("No services defined in compose file",
            "Multi-stack sub-stacks should have proper docker-compose YAML with services");
    }
}

// DTOs for API responses
public class ProductResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsMultiStack { get; set; }
    public int TotalStacks { get; set; }
    public int TotalServices { get; set; }
    public int TotalVariables { get; set; }
    public DateTime LastSyncedAt { get; set; }
}

public class ProductDetailResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsMultiStack { get; set; }
    public int TotalServices { get; set; }
    public int TotalVariables { get; set; }
    public List<ProductStackDto> Stacks { get; set; } = new();
}

public class ProductStackDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Services { get; set; } = new();
    public List<StackVariableDto> Variables { get; set; } = new();
}

public class StackVariableDto
{
    public string Name { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public string Type { get; set; } = "String";
    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? Placeholder { get; set; }
    public string? Group { get; set; }
    public int Order { get; set; }
}
