using FluentAssertions;
using ReadyStackGo.Domain.Catalog.Stacks;

namespace ReadyStackGo.UnitTests.Domain.Catalog;

/// <summary>
/// Unit tests for ProductDefinition.
/// </summary>
public class ProductDefinitionTests
{
    private StackDefinition CreateTestStack(string sourceId = "source-1", string name = "test-stack")
    {
        return new StackDefinition(
            sourceId: sourceId,
            name: name,
            yamlContent: "version: '3'\nservices:\n  web:\n    image: nginx",
            services: new[] { "web" },
            variables: new[] { new StackVariable("TEST_VAR", "default") });
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesProductDefinition()
    {
        // Arrange
        var stack = CreateTestStack();

        // Act
        var product = new ProductDefinition(
            sourceId: "source-1",
            name: "wordpress",
            displayName: "WordPress",
            stacks: new[] { stack });

        // Assert
        product.SourceId.Should().Be("source-1");
        product.Name.Should().Be("wordpress");
        product.DisplayName.Should().Be("WordPress");
        product.Stacks.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WithOptionalParameters_SetsAllValues()
    {
        // Arrange
        var stack = CreateTestStack();

        // Act
        var product = new ProductDefinition(
            sourceId: "source-1",
            name: "wordpress",
            displayName: "WordPress",
            stacks: new[] { stack },
            description: "Blog platform",
            productVersion: "6.4.1",
            category: "CMS",
            tags: new[] { "blog", "cms" },
            icon: "wordpress.svg",
            documentation: "https://wordpress.org/docs");

        // Assert
        product.Description.Should().Be("Blog platform");
        product.ProductVersion.Should().Be("6.4.1");
        product.Category.Should().Be("CMS");
        product.Tags.Should().BeEquivalentTo(new[] { "blog", "cms" });
        product.Icon.Should().Be("wordpress.svg");
        product.Documentation.Should().Be("https://wordpress.org/docs");
    }

    [Fact]
    public void Constructor_WithEmptySourceId_ThrowsArgumentException()
    {
        // Arrange
        var stack = CreateTestStack();

        // Act
        var act = () => new ProductDefinition("", "name", "Name", new[] { stack });

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*SourceId*empty*");
    }

    [Fact]
    public void Constructor_WithNullSourceId_ThrowsArgumentException()
    {
        // Arrange
        var stack = CreateTestStack();

        // Act
        var act = () => new ProductDefinition(null!, "name", "Name", new[] { stack });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var stack = CreateTestStack();

        // Act
        var act = () => new ProductDefinition("source", "", "Name", new[] { stack });

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Name*empty*");
    }

    [Fact]
    public void Constructor_WithNoStacks_ThrowsArgumentException()
    {
        // Act
        var act = () => new ProductDefinition("source", "name", "Name", Array.Empty<StackDefinition>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one stack*");
    }

    [Fact]
    public void Constructor_WithNullStacks_ThrowsArgumentException()
    {
        // Act
        var act = () => new ProductDefinition("source", "name", "Name", null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullTags_DefaultsToEmptyList()
    {
        // Arrange
        var stack = CreateTestStack();

        // Act
        var product = new ProductDefinition("source", "name", "Name", new[] { stack }, tags: null);

        // Assert
        product.Tags.Should().BeEmpty();
    }

    #endregion

    #region Id Tests

    [Fact]
    public void Id_ReturnsSourceIdColonName()
    {
        // Arrange
        var stack = CreateTestStack();
        var product = new ProductDefinition("my-source", "my-product", "My Product", new[] { stack });

        // Assert
        product.Id.Should().Be("my-source:my-product");
    }

    #endregion

    #region IsMultiStack Tests

    [Fact]
    public void IsMultiStack_WithSingleStack_ReturnsFalse()
    {
        // Arrange
        var stack = CreateTestStack();
        var product = new ProductDefinition("source", "product", "Product", new[] { stack });

        // Assert
        product.IsMultiStack.Should().BeFalse();
    }

    [Fact]
    public void IsMultiStack_WithMultipleStacks_ReturnsTrue()
    {
        // Arrange
        var stack1 = CreateTestStack(name: "stack-1");
        var stack2 = CreateTestStack(name: "stack-2");
        var product = new ProductDefinition("source", "product", "Product", new[] { stack1, stack2 });

        // Assert
        product.IsMultiStack.Should().BeTrue();
    }

    #endregion

    #region TotalServices Tests

    [Fact]
    public void TotalServices_SumsServicesAcrossAllStacks()
    {
        // Arrange
        var stack1 = new StackDefinition("source", "stack1", "yaml", services: new[] { "web", "db" });
        var stack2 = new StackDefinition("source", "stack2", "yaml", services: new[] { "api" });
        var product = new ProductDefinition("source", "product", "Product", new[] { stack1, stack2 });

        // Assert
        product.TotalServices.Should().Be(3);
    }

    [Fact]
    public void TotalServices_WithNoServices_ReturnsZero()
    {
        // Arrange
        var stack = new StackDefinition("source", "stack", "yaml");
        var product = new ProductDefinition("source", "product", "Product", new[] { stack });

        // Assert
        product.TotalServices.Should().Be(0);
    }

    #endregion

    #region TotalVariables Tests

    [Fact]
    public void TotalVariables_SumsVariablesAcrossAllStacks()
    {
        // Arrange
        var stack1 = new StackDefinition("source", "stack1", "yaml",
            variables: new[] { new StackVariable("VAR1"), new StackVariable("VAR2") });
        var stack2 = new StackDefinition("source", "stack2", "yaml",
            variables: new[] { new StackVariable("VAR3") });
        var product = new ProductDefinition("source", "product", "Product", new[] { stack1, stack2 });

        // Assert
        product.TotalVariables.Should().Be(3);
    }

    #endregion

    #region LastSyncedAt Tests

    [Fact]
    public void LastSyncedAt_ReturnsMaxSyncTimeAcrossStacks()
    {
        // Arrange
        var earlierTime = DateTime.UtcNow.AddHours(-2);
        var laterTime = DateTime.UtcNow.AddHours(-1);

        var stack1 = new StackDefinition("source", "stack1", "yaml", lastSyncedAt: earlierTime);
        var stack2 = new StackDefinition("source", "stack2", "yaml", lastSyncedAt: laterTime);
        var product = new ProductDefinition("source", "product", "Product", new[] { stack1, stack2 });

        // Assert
        product.LastSyncedAt.Should().Be(laterTime);
    }

    #endregion
}
