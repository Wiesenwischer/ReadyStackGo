using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Stacks;
using ReadyStackGo.Infrastructure.Caching;
using Xunit;

namespace ReadyStackGo.UnitTests.Infrastructure.Caching;

public class InMemoryProductCacheTests
{
    private readonly InMemoryProductCache _cache = new();

    #region Helper Methods

    private static ProductDefinition CreateProduct(
        string sourceId,
        string name,
        string? version = null,
        string? explicitProductId = null)
    {
        var stack = new StackDefinition(
            sourceId: sourceId,
            name: name,
            productId: new ProductId(explicitProductId ?? name),
            services: new List<ServiceTemplate>
            {
                new() { Name = "test-service", Image = "test:latest" }
            },
            productName: name,
            productDisplayName: name,
            productVersion: version);

        return new ProductDefinition(
            sourceId: sourceId,
            name: name,
            displayName: name,
            stacks: new[] { stack },
            productVersion: version,
            productId: explicitProductId);
    }

    #endregion

    #region Basic Operations

    [Fact]
    public void Set_AddsProductToCache()
    {
        // Arrange
        var product = CreateProduct("local", "Whoami", "1.0.0");

        // Act
        _cache.Set(product);

        // Assert
        _cache.ProductCount.Should().Be(1);
        _cache.ProductGroupCount.Should().Be(1);
    }

    [Fact]
    public void GetAllProducts_ReturnsLatestVersionOnly()
    {
        // Arrange
        var v1 = CreateProduct("local", "Whoami", "1.0.0");
        var v2 = CreateProduct("local", "Whoami", "2.0.0");
        var v3 = CreateProduct("local", "Whoami", "3.0.0");

        _cache.Set(v1);
        _cache.Set(v2);
        _cache.Set(v3);

        // Act
        var products = _cache.GetAllProducts().ToList();

        // Assert
        products.Should().HaveCount(1);
        products[0].ProductVersion.Should().Be("3.0.0");
    }

    [Fact]
    public void Clear_RemovesAllProducts()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));
        _cache.Set(CreateProduct("local", "PostgreSQL", "15.0.0"));

        // Act
        _cache.Clear();

        // Assert
        _cache.ProductCount.Should().Be(0);
        _cache.ProductGroupCount.Should().Be(0);
    }

    #endregion

    #region Multi-Version Support

    [Fact]
    public void Set_MultipleVersions_StoresAllVersions()
    {
        // Arrange
        var v1 = CreateProduct("local", "Whoami", "1.0.0");
        var v2 = CreateProduct("local", "Whoami", "2.0.0");
        var v3 = CreateProduct("local", "Whoami", "3.0.0");

        // Act
        _cache.Set(v1);
        _cache.Set(v2);
        _cache.Set(v3);

        // Assert
        _cache.ProductCount.Should().Be(3);
        _cache.ProductGroupCount.Should().Be(1);
    }

    [Fact]
    public void GetProductVersions_ReturnsAllVersionsSortedDescending()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "3.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));

        // Act
        var versions = _cache.GetProductVersions("local:Whoami").ToList();

        // Assert
        versions.Should().HaveCount(3);
        versions[0].ProductVersion.Should().Be("3.0.0");
        versions[1].ProductVersion.Should().Be("2.0.0");
        versions[2].ProductVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void GetProductVersion_ReturnsSpecificVersion()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "3.0.0"));

        // Act
        var product = _cache.GetProductVersion("local:Whoami", "2.0.0");

        // Assert
        product.Should().NotBeNull();
        product!.ProductVersion.Should().Be("2.0.0");
    }

    [Fact]
    public void GetLatestProductVersion_ReturnsNewestVersion()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.5.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));

        // Act
        var latest = _cache.GetLatestProductVersion("local:Whoami");

        // Assert
        latest.Should().NotBeNull();
        latest!.ProductVersion.Should().Be("2.5.0");
    }

    [Fact]
    public void GetAvailableUpgrades_ReturnsOnlyHigherVersions()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "3.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "4.0.0"));

        // Act
        var upgrades = _cache.GetAvailableUpgrades("local:Whoami", "2.0.0").ToList();

        // Assert
        upgrades.Should().HaveCount(2);
        upgrades[0].ProductVersion.Should().Be("4.0.0");
        upgrades[1].ProductVersion.Should().Be("3.0.0");
    }

    [Fact]
    public void GetAvailableUpgrades_ReturnsEmptyWhenOnLatest()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));

        // Act
        var upgrades = _cache.GetAvailableUpgrades("local:Whoami", "2.0.0").ToList();

        // Assert
        upgrades.Should().BeEmpty();
    }

    #endregion

    #region ProductId Grouping

    [Fact]
    public void Set_WithExplicitProductId_GroupsByProductId()
    {
        // Arrange - Same productId but different sources
        var localV1 = CreateProduct("local", "Whoami", "1.0.0", "com.example.whoami");
        var gitV2 = CreateProduct("git:github.com/org/repo", "Whoami", "2.0.0", "com.example.whoami");

        // Act
        _cache.Set(localV1);
        _cache.Set(gitV2);

        // Assert - Should be grouped together
        _cache.ProductGroupCount.Should().Be(1);
        _cache.ProductCount.Should().Be(2);

        var versions = _cache.GetProductVersions("com.example.whoami").ToList();
        versions.Should().HaveCount(2);
    }

    [Fact]
    public void Set_WithDifferentProductIds_CreatesSeperateGroups()
    {
        // Arrange - Different productIds, same name
        var product1 = CreateProduct("local", "Whoami", "1.0.0", "com.company1.whoami");
        var product2 = CreateProduct("local", "Whoami", "1.0.0", "com.company2.whoami");

        // Act
        _cache.Set(product1);
        _cache.Set(product2);

        // Assert - Should be separate groups
        _cache.ProductGroupCount.Should().Be(2);
        _cache.ProductCount.Should().Be(2);
    }

    [Fact]
    public void Set_WithoutProductId_FallsBackToSourceIdName()
    {
        // Arrange - No productId
        var product = CreateProduct("local", "Whoami", "1.0.0");

        // Act
        _cache.Set(product);

        // Assert - GroupId should be sourceId:name
        product.GroupId.Should().Be("local:Whoami");
        var versions = _cache.GetProductVersions("local:Whoami").ToList();
        versions.Should().HaveCount(1);
    }

    [Fact]
    public void GetAvailableUpgrades_WorksAcrossSources()
    {
        // Arrange - Same product from different sources
        var localV1 = CreateProduct("local", "Whoami", "1.0.0", "com.example.whoami");
        var gitV2 = CreateProduct("git:github.com/org/repo", "Whoami", "2.0.0", "com.example.whoami");
        var gitV3 = CreateProduct("git:github.com/org/repo", "Whoami", "3.0.0", "com.example.whoami");

        _cache.Set(localV1);
        _cache.Set(gitV2);
        _cache.Set(gitV3);

        // Act - Get upgrades from v1
        var upgrades = _cache.GetAvailableUpgrades("com.example.whoami", "1.0.0").ToList();

        // Assert - Should include versions from both sources
        upgrades.Should().HaveCount(2);
        upgrades[0].ProductVersion.Should().Be("3.0.0");
        upgrades[0].SourceId.Should().Be("git:github.com/org/repo");
    }

    #endregion

    #region Remove Operations

    [Fact]
    public void Remove_RemovesSpecificVersion()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));

        // Act
        _cache.Remove("local:Whoami:2.0.0");

        // Assert
        _cache.ProductCount.Should().Be(1);
        var versions = _cache.GetProductVersions("local:Whoami").ToList();
        versions.Should().HaveCount(1);
        versions[0].ProductVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void RemoveGroup_RemovesAllVersions()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "3.0.0"));

        // Act
        _cache.RemoveGroup("local:Whoami");

        // Assert
        _cache.ProductCount.Should().Be(0);
        _cache.ProductGroupCount.Should().Be(0);
    }

    [Fact]
    public void RemoveBySource_RemovesOnlyFromSpecificSource()
    {
        // Arrange
        var localV1 = CreateProduct("local", "Whoami", "1.0.0", "com.example.whoami");
        var gitV2 = CreateProduct("git", "Whoami", "2.0.0", "com.example.whoami");

        _cache.Set(localV1);
        _cache.Set(gitV2);

        // Act
        _cache.RemoveBySource("local");

        // Assert
        _cache.ProductCount.Should().Be(1);
        var versions = _cache.GetProductVersions("com.example.whoami").ToList();
        versions.Should().HaveCount(1);
        versions[0].SourceId.Should().Be("git");
    }

    #endregion

    #region Version Comparison

    [Fact]
    public void SemVerComparison_HandlesVPrefix()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "v1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "v2.0.0"));

        // Act
        var latest = _cache.GetLatestProductVersion("local:Whoami");
        var version = _cache.GetProductVersion("local:Whoami", "1.0.0");

        // Assert
        latest!.ProductVersion.Should().Be("v2.0.0");
        version.Should().NotBeNull();
    }

    [Fact]
    public void SemVerComparison_HandlesPatchVersions()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "1.0.1"));
        _cache.Set(CreateProduct("local", "Whoami", "1.1.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));

        // Act
        var upgrades = _cache.GetAvailableUpgrades("local:Whoami", "1.0.0").ToList();

        // Assert
        upgrades.Should().HaveCount(3);
        upgrades[0].ProductVersion.Should().Be("2.0.0");
        upgrades[1].ProductVersion.Should().Be("1.1.0");
        upgrades[2].ProductVersion.Should().Be("1.0.1");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetProductVersions_NonExistentGroup_ReturnsEmpty()
    {
        // Act
        var versions = _cache.GetProductVersions("nonexistent:group");

        // Assert
        versions.Should().BeEmpty();
    }

    [Fact]
    public void GetProductVersion_NonExistentVersion_ReturnsNull()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));

        // Act
        var product = _cache.GetProductVersion("local:Whoami", "99.0.0");

        // Assert
        product.Should().BeNull();
    }

    [Fact]
    public void GetProduct_ByOldFormatId_ReturnsLatestVersion()
    {
        // Arrange
        _cache.Set(CreateProduct("local", "Whoami", "1.0.0"));
        _cache.Set(CreateProduct("local", "Whoami", "2.0.0"));

        // Act - Use old format (without version)
        var product = _cache.GetProduct("local:Whoami");

        // Assert - Should return latest version
        product.Should().NotBeNull();
        product!.ProductVersion.Should().Be("2.0.0");
    }

    [Fact]
    public void Set_ProductWithoutVersion_UsesLatestAsKey()
    {
        // Arrange
        var product = CreateProduct("local", "Whoami");

        // Act
        _cache.Set(product);

        // Assert
        _cache.ProductCount.Should().Be(1);
        var versions = _cache.GetProductVersions("local:Whoami").ToList();
        versions.Should().HaveCount(1);
        versions[0].ProductVersion.Should().BeNull();
    }

    [Fact]
    public void GetProduct_BySourceIdName_WhenExplicitProductIdSet_ReturnsProduct()
    {
        // Arrange - Product with explicit productId (different from sourceId:name)
        var product = CreateProduct("stacks", "Whoami", "1.0.0", "io.traefik.whoami");
        _cache.Set(product);

        // Act - Search by old format (sourceId:name) even though productId is different
        var found = _cache.GetProduct("stacks:Whoami");

        // Assert - Should find the product via fallback search
        found.Should().NotBeNull();
        found!.ProductVersion.Should().Be("1.0.0");
        found.GroupId.Should().Be("io.traefik.whoami");
    }

    #endregion
}
