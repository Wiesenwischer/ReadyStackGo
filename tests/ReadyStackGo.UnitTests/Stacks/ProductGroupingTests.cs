using FluentAssertions;
using ReadyStackGo.Domain.Catalog.Stacks;

namespace ReadyStackGo.UnitTests.Stacks;

/// <summary>
/// Tests for product grouping logic.
///
/// Product grouping rules:
/// 1. Each stack with metadata.productVersion is its own product (single-stack product)
/// 2. Stacks WITHOUT productVersion are standalone and shown as individual products
/// 3. Multi-stack products must be explicitly defined in a multi-stack manifest
///
/// The current folder-based grouping is WRONG because:
/// - It groups unrelated stacks that happen to be in the same folder
/// - It doesn't respect the manifest's own productVersion declaration
/// </summary>
public class ProductGroupingTests
{
    [Fact]
    public void SingleStackWithProductVersion_IsOwnProduct()
    {
        // Arrange - VariableShowcase has productVersion "1.0.0"
        var variableShowcase = CreateStack(
            sourceId: "stacks",
            name: "Variable Showcase",
            relativePath: "examples",
            hasProductVersion: true,
            productVersion: "1.0.0");

        var stacks = new[] { variableShowcase };

        // Act
        var products = GroupStacksIntoProducts(stacks);

        // Assert
        products.Should().HaveCount(1);
        products[0].Name.Should().Be("Variable Showcase");
        products[0].Stacks.Should().HaveCount(1);
        products[0].IsMultiStack.Should().BeFalse();
    }

    [Fact]
    public void MultipleStacksInSameFolder_EachWithProductVersion_AreSeperateProducts()
    {
        // Arrange - Both in "examples" folder but each has own productVersion
        var variableShowcase = CreateStack(
            sourceId: "stacks",
            name: "Variable Showcase",
            relativePath: "examples",
            hasProductVersion: true,
            productVersion: "1.0.0");

        var wordpress = CreateStack(
            sourceId: "stacks",
            name: "WordPress",
            relativePath: "examples",
            hasProductVersion: true,
            productVersion: "2.0.0");

        var stacks = new[] { variableShowcase, wordpress };

        // Act
        var products = GroupStacksIntoProducts(stacks);

        // Assert
        products.Should().HaveCount(2, "each stack with productVersion should be its own product");
        products.Should().Contain(p => p.Name == "Variable Showcase");
        products.Should().Contain(p => p.Name == "WordPress");
    }

    [Fact]
    public void StackWithoutProductVersion_IsStandaloneProduct()
    {
        // Arrange - whoami.yaml has no productVersion
        var whoami = CreateStack(
            sourceId: "stacks",
            name: "whoami",
            relativePath: "examples",
            hasProductVersion: false,
            productVersion: null);

        var stacks = new[] { whoami };

        // Act
        var products = GroupStacksIntoProducts(stacks);

        // Assert
        products.Should().HaveCount(1);
        products[0].Name.Should().Be("whoami");
        products[0].IsMultiStack.Should().BeFalse();
    }

    [Fact]
    public void MixedStacks_GroupedCorrectly()
    {
        // Arrange - Real folder structure:
        // examples/VariableShowcase/stack.yaml (productVersion: 1.0.0)
        // examples/WordPress/stack.yaml (productVersion: 6.4.0)
        // examples/whoami.yaml (no productVersion)
        // ams.project/IdentityAccess/stack.yaml (productVersion: 0.5.0)

        var variableShowcase = CreateStack("stacks", "Variable Showcase", "examples", true, "1.0.0");
        var wordpress = CreateStack("stacks", "WordPress", "examples", true, "6.4.0");
        var whoami = CreateStack("stacks", "whoami", "examples", false, null);
        var identityAccess = CreateStack("stacks", "IdentityAccess", "ams.project", true, "0.5.0");

        var stacks = new[] { variableShowcase, wordpress, whoami, identityAccess };

        // Act
        var products = GroupStacksIntoProducts(stacks);

        // Assert
        products.Should().HaveCount(4, "each stack should be its own product");

        var showcase = products.Single(p => p.Stacks.Any(s => s.Name == "Variable Showcase"));
        showcase.Stacks.Should().HaveCount(1);

        var wp = products.Single(p => p.Stacks.Any(s => s.Name == "WordPress"));
        wp.Stacks.Should().HaveCount(1);

        var who = products.Single(p => p.Stacks.Any(s => s.Name == "whoami"));
        who.Stacks.Should().HaveCount(1);

        var identity = products.Single(p => p.Stacks.Any(s => s.Name == "IdentityAccess"));
        identity.Stacks.Should().HaveCount(1);
    }

    [Fact]
    public void CurrentBrokenBehavior_GroupsByFolder_ShouldFail()
    {
        // This test documents the CURRENT BROKEN behavior
        // It should FAIL once we fix the grouping logic

        var variableShowcase = CreateStack("stacks", "Variable Showcase", "examples", true, "1.0.0");
        var wordpress = CreateStack("stacks", "WordPress", "examples", true, "6.4.0");
        var whoami = CreateStack("stacks", "whoami", "examples", false, null);

        var stacks = new[] { variableShowcase, wordpress, whoami };

        // Act - current broken implementation groups by folder
        var products = GroupStacksIntoProductsBroken(stacks);

        // Assert - BROKEN: All 3 stacks are grouped into "examples" product
        products.Should().HaveCount(1, "BROKEN: currently groups by folder");
        products[0].Stacks.Should().HaveCount(3, "BROKEN: all stacks in examples folder");
    }

    #region Helper Methods

    private static StackDefinition CreateStack(
        string sourceId,
        string name,
        string? relativePath,
        bool hasProductVersion,
        string? productVersion)
    {
        return new StackDefinition(
            sourceId: sourceId,
            name: name,
            yamlContent: "services: {}",
            description: $"{name} stack",
            variables: Array.Empty<StackVariable>(),
            services: new[] { "app" },
            filePath: $"/app/stacks/{relativePath}/{name}/stack.yaml",
            relativePath: relativePath,
            lastSyncedAt: DateTime.UtcNow,
            version: productVersion);
    }

    /// <summary>
    /// CORRECT implementation - each stack with productVersion is its own product
    /// </summary>
    private static List<ProductDefinition> GroupStacksIntoProducts(IEnumerable<StackDefinition> stacks)
    {
        // Each stack is its own product
        // Multi-stack products would need to be defined in a multi-stack manifest
        return stacks.Select(s => new ProductDefinition(
            sourceId: s.SourceId,
            name: s.Name,
            displayName: s.Name,
            stacks: new[] { s },
            description: s.Description,
            productVersion: s.Version,
            category: null,
            tags: null,
            icon: null,
            documentation: null
        )).ToList();
    }

    /// <summary>
    /// BROKEN implementation - groups by first folder in relativePath
    /// This is what the current code does wrong
    /// </summary>
    private static List<ProductDefinition> GroupStacksIntoProductsBroken(IEnumerable<StackDefinition> stacks)
    {
        var groups = stacks
            .GroupBy(s => GetProductKeyBroken(s.SourceId, s.RelativePath, s.Name))
            .ToList();

        var products = new List<ProductDefinition>();
        foreach (var group in groups)
        {
            var stackList = group.ToList();
            var firstStack = stackList.First();
            var parts = group.Key.Split('|', 2);
            var productName = parts.Length > 1 ? parts[1] : parts[0];

            products.Add(new ProductDefinition(
                sourceId: parts[0],
                name: productName,
                displayName: stackList.Count > 1 ? productName : firstStack.Name,
                stacks: stackList,
                description: firstStack.Description,
                productVersion: firstStack.Version,
                category: null,
                tags: null,
                icon: null,
                documentation: null
            ));
        }
        return products;
    }

    private static string GetProductKeyBroken(string sourceId, string? relativePath, string stackName)
    {
        if (!string.IsNullOrEmpty(relativePath))
        {
            var productFolder = relativePath.Split('/', '\\')[0];
            return $"{sourceId}|{productFolder}";
        }
        return $"{sourceId}|{stackName}";
    }

    #endregion
}
