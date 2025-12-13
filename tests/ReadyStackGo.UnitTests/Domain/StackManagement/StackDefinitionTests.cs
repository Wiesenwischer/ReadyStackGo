using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Domain.StackManagement;

/// <summary>
/// Unit tests for StackDefinition domain class.
/// v0.12: Updated to use structured ServiceTemplate data instead of YAML strings.
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
        var services = new[]
        {
            CreateServiceTemplate("mysql", "mysql:8"),
            CreateServiceTemplate("adminer", "adminer:latest")
        };

        // Act
        var stack = new StackDefinition(
            sourceId: "local",
            name: "MySQL Stack",
            services: services,
            description: "MySQL database stack",
            variables: variables,
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
        // Arrange
        var services = new[] { CreateServiceTemplate("web", "nginx") };

        // Act
        var act = () => new StackDefinition(
            sourceId: "",
            name: "Test",
            services: services);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*sourceId*");
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var services = new[] { CreateServiceTemplate("web", "nginx") };

        // Act
        var act = () => new StackDefinition(
            sourceId: "local",
            name: "",
            services: services);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Constructor_WithNullServices_CreatesEmptyServicesList()
    {
        // Act
        var stack = new StackDefinition(
            sourceId: "local",
            name: "Test",
            services: null);

        // Assert
        stack.Services.Should().NotBeNull();
        stack.Services.Should().BeEmpty();
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
        stack.Variables.Should().BeAssignableTo<IReadOnlyList<Variable>>();
    }

    [Fact]
    public void GetRequiredVariables_ReturnsOnlyRequired()
    {
        // Arrange - REQUIRED_VAR has no default, so it's required
        // OPTIONAL_VAR has a default value, so it's optional
        var variables = new[]
        {
            new Variable("REQUIRED_VAR"),
            new Variable("OPTIONAL_VAR", "default")
        };
        var services = new[] { CreateServiceTemplate("web", "nginx") };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services,
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
            new Variable("REQUIRED_VAR"),
            new Variable("OPTIONAL_VAR", "default")
        };
        var services = new[] { CreateServiceTemplate("web", "nginx") };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services,
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
        stack.Services.Should().BeAssignableTo<IReadOnlyList<ServiceTemplate>>();
    }

    [Fact]
    public void GetServiceNames_ReturnsServiceNames()
    {
        // Arrange
        var services = new[]
        {
            CreateServiceTemplate("mysql", "mysql:8"),
            CreateServiceTemplate("adminer", "adminer:latest")
        };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services);

        // Act
        var names = stack.GetServiceNames().ToList();

        // Assert
        names.Should().HaveCount(2);
        names.Should().Contain("mysql");
        names.Should().Contain("adminer");
    }

    [Fact]
    public void HasService_ExistingService_ReturnsTrue()
    {
        // Arrange
        var services = new[]
        {
            CreateServiceTemplate("mysql", "mysql:8"),
            CreateServiceTemplate("adminer", "adminer:latest")
        };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
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
        var services = new[]
        {
            CreateServiceTemplate("mysql", "mysql:8"),
            CreateServiceTemplate("adminer", "adminer:latest")
        };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services);

        // Act
        var result = stack.HasService("postgres");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetService_ExistingService_ReturnsServiceTemplate()
    {
        // Arrange
        var services = new[]
        {
            CreateServiceTemplate("mysql", "mysql:8"),
            CreateServiceTemplate("adminer", "adminer:latest")
        };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services);

        // Act
        var service = stack.GetService("mysql");

        // Assert
        service.Should().NotBeNull();
        service!.Name.Should().Be("mysql");
        service.Image.Should().Be("mysql:8");
    }

    [Fact]
    public void GetService_NonExistentService_ReturnsNull()
    {
        // Arrange
        var services = new[] { CreateServiceTemplate("mysql", "mysql:8") };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services);

        // Act
        var service = stack.GetService("postgres");

        // Assert
        service.Should().BeNull();
    }

    #endregion

    #region Volumes and Networks Tests

    [Fact]
    public void Volumes_DefaultsToEmpty()
    {
        // Arrange
        var services = new[] { CreateServiceTemplate("web", "nginx") };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services);

        // Assert
        stack.Volumes.Should().BeEmpty();
    }

    [Fact]
    public void Networks_DefaultsToEmpty()
    {
        // Arrange
        var services = new[] { CreateServiceTemplate("web", "nginx") };
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services);

        // Assert
        stack.Networks.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithVolumesAndNetworks_SetsCollections()
    {
        // Arrange
        var services = new[] { CreateServiceTemplate("db", "postgres:15") };
        var volumes = new[]
        {
            new VolumeDefinition { Name = "db_data" }
        };
        var networks = new[]
        {
            new NetworkDefinition { Name = "backend" }
        };

        // Act
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            services: services,
            volumes: volumes,
            networks: networks);

        // Assert
        stack.Volumes.Should().HaveCount(1);
        stack.Volumes.First().Name.Should().Be("db_data");
        stack.Networks.Should().HaveCount(1);
        stack.Networks.First().Name.Should().Be("backend");
    }

    #endregion

    #region Helper Methods

    private static StackDefinition CreateTestStack(string sourceId, string name)
    {
        return new StackDefinition(
            sourceId: sourceId,
            name: name,
            services: new[] { CreateServiceTemplate("web", "nginx") },
            description: "Test stack",
            filePath: $"/stacks/{name}/stack.yaml",
            relativePath: name,
            version: "1.0.0");
    }

    private static ServiceTemplate CreateServiceTemplate(string name, string image)
    {
        return new ServiceTemplate
        {
            Name = name,
            Image = image
        };
    }

    private static Variable CreateVariable(string name, string? defaultValue)
    {
        return new Variable(name, defaultValue);
    }

    #endregion
}
