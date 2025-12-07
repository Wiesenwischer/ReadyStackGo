using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.StackSources;

namespace ReadyStackGo.UnitTests.Domain.StackManagement;

/// <summary>
/// Unit tests for StackDefinition domain class.
/// </summary>
public class StackDefinitionTests
{
    #region Creation Tests

    [Fact]
    public void Constructor_WithValidData_CreatesStackDefinition()
    {
        // Arrange
        var variables = new[]
        {
            CreateVariable("DB_HOST", "localhost"),
            CreateVariable("DB_PORT", "3306")
        };
        var services = new[] { "mysql", "adminer" };

        // Act
        var stack = new StackDefinition(
            sourceId: "local",
            name: "MySQL Stack",
            yamlContent: "services:\n  mysql:\n    image: mysql:8",
            description: "MySQL database stack",
            variables: variables,
            services: services,
            filePath: "/stacks/mysql/stack.yaml",
            relativePath: "mysql",
            lastSyncedAt: DateTime.UtcNow,
            version: "1.0.0");

        // Assert
        stack.SourceId.Should().Be("local");
        stack.Name.Should().Be("MySQL Stack");
        stack.Description.Should().Be("MySQL database stack");
        stack.Variables.Should().HaveCount(2);
        stack.Services.Should().HaveCount(2);
        stack.FilePath.Should().Be("/stacks/mysql/stack.yaml");
        stack.RelativePath.Should().Be("mysql");
        stack.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void Constructor_WithEmptySourceId_ThrowsArgumentException()
    {
        // Act
        var act = () => new StackDefinition(
            sourceId: "",
            name: "Test",
            yamlContent: "services: {}");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*sourceId*");
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => new StackDefinition(
            sourceId: "local",
            name: "",
            yamlContent: "services: {}");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Constructor_WithEmptyYamlContent_ThrowsArgumentException()
    {
        // Act
        var act = () => new StackDefinition(
            sourceId: "local",
            name: "Test",
            yamlContent: "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*yamlContent*");
    }

    #endregion

    #region Id Generation Tests

    [Fact]
    public void Id_GeneratedFromSourceAndName()
    {
        // Arrange
        var stack = CreateTestStack("local", "wordpress");

        // Act
        var id = stack.Id;

        // Assert
        id.Should().Be("local:wordpress");
    }

    [Fact]
    public void Id_SameSourceAndName_GeneratesSameId()
    {
        // Arrange
        var stack1 = CreateTestStack("local", "wordpress");
        var stack2 = CreateTestStack("local", "wordpress");

        // Act & Assert
        stack1.Id.Should().Be(stack2.Id);
    }

    [Fact]
    public void Id_DifferentSource_GeneratesDifferentId()
    {
        // Arrange
        var stack1 = CreateTestStack("local", "wordpress");
        var stack2 = CreateTestStack("remote", "wordpress");

        // Act & Assert
        stack1.Id.Should().NotBe(stack2.Id);
    }

    #endregion

    #region Variable Tests

    [Fact]
    public void Variables_IsReadOnly()
    {
        // Arrange
        var stack = CreateTestStack("local", "test");

        // Act & Assert
        stack.Variables.Should().BeAssignableTo<IReadOnlyList<StackVariable>>();
    }

    [Fact]
    public void GetRequiredVariables_ReturnsOnlyRequired()
    {
        // Arrange - REQUIRED_VAR has no default, so it's required
        // OPTIONAL_VAR has a default value, so it's optional
        var variables = new[]
        {
            new StackVariable("REQUIRED_VAR"),
            new StackVariable("OPTIONAL_VAR", "default")
        };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            yamlContent: "services: {}",
            variables: variables);

        // Act
        var required = stack.GetRequiredVariables();

        // Assert
        required.Should().HaveCount(1);
        required.First().Name.Should().Be("REQUIRED_VAR");
    }

    [Fact]
    public void GetOptionalVariables_ReturnsOnlyOptional()
    {
        // Arrange - REQUIRED_VAR has no default, so it's required
        // OPTIONAL_VAR has a default value, so it's optional
        var variables = new[]
        {
            new StackVariable("REQUIRED_VAR"),
            new StackVariable("OPTIONAL_VAR", "default")
        };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            yamlContent: "services: {}",
            variables: variables);

        // Act
        var optional = stack.GetOptionalVariables();

        // Assert
        optional.Should().HaveCount(1);
        optional.First().Name.Should().Be("OPTIONAL_VAR");
    }

    #endregion

    #region Service Tests

    [Fact]
    public void Services_IsReadOnly()
    {
        // Arrange
        var stack = CreateTestStack("local", "test");

        // Act & Assert
        stack.Services.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    [Fact]
    public void HasService_ExistingService_ReturnsTrue()
    {
        // Arrange
        var services = new[] { "mysql", "adminer" };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            yamlContent: "services: {}",
            services: services);

        // Act
        var result = stack.HasService("mysql");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasService_NonExistentService_ReturnsFalse()
    {
        // Arrange
        var services = new[] { "mysql", "adminer" };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            yamlContent: "services: {}",
            services: services);

        // Act
        var result = stack.HasService("postgres");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static StackDefinition CreateTestStack(string sourceId, string name)
    {
        return new StackDefinition(
            sourceId: sourceId,
            name: name,
            yamlContent: "services:\n  web:\n    image: nginx",
            description: "Test stack",
            services: new[] { "web" },
            filePath: $"/stacks/{name}/stack.yaml",
            relativePath: name,
            version: "1.0.0");
    }

    private static StackVariable CreateVariable(string name, string? defaultValue)
    {
        return new StackVariable(name, defaultValue);
    }

    #endregion
}
