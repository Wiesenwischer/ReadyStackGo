using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Domain.StackManagement;

/// <summary>
/// Unit tests for StackId value object.
/// </summary>
public class StackIdTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var sourceId = "stacks";
        var productId = ProductId.FromName("io.traefik.whoami");
        var version = "1.0.0";
        var stackName = "Whoami";

        // Act
        var stackId = new StackId(sourceId, productId, version, stackName);

        // Assert
        stackId.SourceId.Should().Be(sourceId);
        stackId.ProductId.Should().Be(productId);
        stackId.Version.Should().Be(version);
        stackId.StackName.Should().Be(stackName);
    }

    #endregion

    #region Value Generation Tests

    [Fact]
    public void Value_WithVersion_IncludesAllComponents()
    {
        // Arrange
        var stackId = new StackId("stacks", ProductId.FromName("io.traefik.whoami"), "1.0.0", "Whoami");

        // Act
        var value = stackId.Value;

        // Assert
        value.Should().Be("stacks:io.traefik.whoami:1.0.0:Whoami");
    }

    [Fact]
    public void Value_WithoutVersion_ExcludesVersionPart()
    {
        // Arrange
        var stackId = new StackId("stacks", ProductId.FromName("io.traefik.whoami"), null, "Whoami");

        // Act
        var value = stackId.Value;

        // Assert
        value.Should().Be("stacks:io.traefik.whoami:Whoami");
    }

    [Fact]
    public void Value_WithEmptyVersion_ExcludesVersionPart()
    {
        // Arrange
        var stackId = new StackId("stacks", ProductId.FromName("io.traefik.whoami"), "", "Whoami");

        // Act
        var value = stackId.Value;

        // Assert
        value.Should().Be("stacks:io.traefik.whoami:Whoami");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var stackId = new StackId("stacks", ProductId.FromName("io.traefik.whoami"), "1.0.0", "Whoami");

        // Act
        var result = stackId.ToString();

        // Assert
        result.Should().Be("stacks:io.traefik.whoami:1.0.0:Whoami");
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public void TryParse_ValidThreePartString_ReturnsTrue()
    {
        // Act
        var success = StackId.TryParse("stacks:wordpress:WordPress", out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.SourceId.Should().Be("stacks");
        result.ProductId.Value.Should().Be("wordpress");
        result.Version.Should().BeNull();
        result.StackName.Should().Be("WordPress");
    }

    [Fact]
    public void TryParse_ValidFourPartString_ReturnsTrue()
    {
        // Act
        var success = StackId.TryParse("stacks:io.traefik.whoami:1.0.0:Whoami", out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.SourceId.Should().Be("stacks");
        result.ProductId.Value.Should().Be("io.traefik.whoami");
        result.Version.Should().Be("1.0.0");
        result.StackName.Should().Be("Whoami");
    }

    [Fact]
    public void TryParse_NullString_ReturnsFalse()
    {
        // Act
        var success = StackId.TryParse(null, out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        // Act
        var success = StackId.TryParse("", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_WhitespaceString_ReturnsFalse()
    {
        // Act
        var success = StackId.TryParse("   ", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_TwoPartString_ReturnsFalse()
    {
        // Act
        var success = StackId.TryParse("stacks:wordpress", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_FivePartString_ReturnsFalse()
    {
        // Act
        var success = StackId.TryParse("stacks:product:1.0.0:stack:extra", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_OnePartString_ReturnsFalse()
    {
        // Act
        var success = StackId.TryParse("justastring", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_RoundTrip_WithVersion_PreservesValue()
    {
        // Arrange
        var original = new StackId("stacks", ProductId.FromName("io.traefik.whoami"), "2.0.0", "Whoami");
        var serialized = original.Value;

        // Act
        var success = StackId.TryParse(serialized, out var parsed);

        // Assert
        success.Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.Value.Should().Be(original.Value);
        parsed.SourceId.Should().Be(original.SourceId);
        parsed.ProductId.Value.Should().Be(original.ProductId.Value);
        parsed.Version.Should().Be(original.Version);
        parsed.StackName.Should().Be(original.StackName);
    }

    [Fact]
    public void TryParse_RoundTrip_WithoutVersion_PreservesValue()
    {
        // Arrange
        var original = new StackId("local", ProductId.FromName("wordpress"), null, "WordPress");
        var serialized = original.Value;

        // Act
        var success = StackId.TryParse(serialized, out var parsed);

        // Assert
        success.Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.Value.Should().Be(original.Value);
        parsed.SourceId.Should().Be(original.SourceId);
        parsed.ProductId.Value.Should().Be(original.ProductId.Value);
        parsed.Version.Should().BeNull();
        parsed.StackName.Should().Be(original.StackName);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var stackId1 = new StackId("stacks", ProductId.FromName("wordpress"), "1.0.0", "WordPress");
        var stackId2 = new StackId("stacks", ProductId.FromName("wordpress"), "1.0.0", "WordPress");

        // Assert
        stackId1.Should().Be(stackId2);
        stackId1.Value.Should().Be(stackId2.Value);
    }

    [Fact]
    public void Equality_DifferentSource_AreNotEqual()
    {
        // Arrange
        var stackId1 = new StackId("stacks", ProductId.FromName("wordpress"), "1.0.0", "WordPress");
        var stackId2 = new StackId("remote", ProductId.FromName("wordpress"), "1.0.0", "WordPress");

        // Assert
        stackId1.Should().NotBe(stackId2);
    }

    [Fact]
    public void Equality_DifferentVersion_AreNotEqual()
    {
        // Arrange
        var stackId1 = new StackId("stacks", ProductId.FromName("wordpress"), "1.0.0", "WordPress");
        var stackId2 = new StackId("stacks", ProductId.FromName("wordpress"), "2.0.0", "WordPress");

        // Assert
        stackId1.Should().NotBe(stackId2);
    }

    #endregion
}
