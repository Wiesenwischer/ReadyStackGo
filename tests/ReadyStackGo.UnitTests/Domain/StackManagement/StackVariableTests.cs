using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;

namespace ReadyStackGo.UnitTests.Domain.StackManagement;

/// <summary>
/// Unit tests for StackVariable domain class.
/// </summary>
public class StackVariableTests
{
    #region Creation Tests

    [Fact]
    public void Constructor_Simple_CreatesVariable()
    {
        // Act
        var variable = new StackVariable("DB_HOST", "localhost");

        // Assert
        variable.Name.Should().Be("DB_HOST");
        variable.DefaultValue.Should().Be("localhost");
        variable.IsRequired.Should().BeFalse(); // Has default so not required
        variable.Type.Should().Be(VariableType.String); // Default type
    }

    [Fact]
    public void Constructor_WithoutDefault_IsRequired()
    {
        // Act
        var variable = new StackVariable("DB_PASSWORD");

        // Assert
        variable.Name.Should().Be("DB_PASSWORD");
        variable.DefaultValue.Should().BeNull();
        variable.IsRequired.Should().BeTrue(); // No default so required
    }

    [Fact]
    public void Constructor_ExplicitRequired_OverridesDefault()
    {
        // Act
        var variable = new StackVariable(
            name: "DB_HOST",
            defaultValue: "localhost",
            description: null,
            type: VariableType.String,
            isRequired: true);

        // Assert
        variable.DefaultValue.Should().Be("localhost");
        variable.IsRequired.Should().BeTrue(); // Explicitly required even with default
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => new StackVariable("", "value");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Constructor_Full_CreatesCompleteVariable()
    {
        // Act
        var variable = new StackVariable(
            name: "EMAIL",
            defaultValue: null,
            description: "Admin email address",
            type: VariableType.String,
            label: "Email Address",
            pattern: @"^[\w\.-]+@[\w\.-]+\.\w+$",
            patternError: "Invalid email format",
            options: null,
            min: null,
            max: null,
            placeholder: "admin@example.com",
            group: "Admin Settings",
            order: 1);

        // Assert
        variable.Name.Should().Be("EMAIL");
        variable.Description.Should().Be("Admin email address");
        variable.Type.Should().Be(VariableType.String);
        variable.Label.Should().Be("Email Address");
        variable.Pattern.Should().Be(@"^[\w\.-]+@[\w\.-]+\.\w+$");
        variable.PatternError.Should().Be("Invalid email format");
        variable.Group.Should().Be("Admin Settings");
        variable.Order.Should().Be(1);
        variable.Placeholder.Should().Be("admin@example.com");
        variable.IsRequired.Should().BeTrue();
    }

    #endregion

    #region Type-Specific Tests

    [Fact]
    public void Port_Variable_HasCorrectType()
    {
        // Act
        var variable = new StackVariable(
            name: "APP_PORT",
            defaultValue: "8080",
            description: null,
            type: VariableType.Port,
            min: 1,
            max: 65535);

        // Assert
        variable.Type.Should().Be(VariableType.Port);
        variable.Min.Should().Be(1);
        variable.Max.Should().Be(65535);
    }

    [Fact]
    public void Select_Variable_HasOptions()
    {
        // Arrange
        var options = new[]
        {
            new SelectOption("dev", "Development", "For development use"),
            new SelectOption("prod", "Production", "For production use")
        };

        // Act
        var variable = new StackVariable(
            name: "ENVIRONMENT",
            defaultValue: "dev",
            description: null,
            type: VariableType.Select,
            options: options);

        // Assert
        variable.Type.Should().Be(VariableType.Select);
        variable.Options.Should().HaveCount(2);
        variable.Options!.First().Value.Should().Be("dev");
        variable.Options!.First().Label.Should().Be("Development");
    }

    [Fact]
    public void Boolean_Variable_HasCorrectType()
    {
        // Act
        var variable = new StackVariable(
            name: "ENABLE_DEBUG",
            defaultValue: "false",
            description: null,
            type: VariableType.Boolean);

        // Assert
        variable.Type.Should().Be(VariableType.Boolean);
    }

    [Fact]
    public void Password_Variable_HasCorrectType()
    {
        // Act
        var variable = new StackVariable(
            name: "DB_PASSWORD",
            defaultValue: null,
            description: null,
            type: VariableType.Password);

        // Assert
        variable.Type.Should().Be(VariableType.Password);
        variable.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void ConnectionString_Variables_HaveCorrectTypes()
    {
        // Arrange & Act
        var sqlServer = new StackVariable("SQL_CONN", null, null, VariableType.SqlServerConnectionString);
        var postgres = new StackVariable("PG_CONN", null, null, VariableType.PostgresConnectionString);
        var mysql = new StackVariable("MYSQL_CONN", null, null, VariableType.MySqlConnectionString);
        var mongo = new StackVariable("MONGO_CONN", null, null, VariableType.MongoConnectionString);
        var redis = new StackVariable("REDIS_CONN", null, null, VariableType.RedisConnectionString);
        var eventStore = new StackVariable("ES_CONN", null, null, VariableType.EventStoreConnectionString);

        // Assert
        sqlServer.Type.Should().Be(VariableType.SqlServerConnectionString);
        postgres.Type.Should().Be(VariableType.PostgresConnectionString);
        mysql.Type.Should().Be(VariableType.MySqlConnectionString);
        mongo.Type.Should().Be(VariableType.MongoConnectionString);
        redis.Type.Should().Be(VariableType.RedisConnectionString);
        eventStore.Type.Should().Be(VariableType.EventStoreConnectionString);
    }

    #endregion

    #region WithDefaultValue Tests

    [Fact]
    public void WithDefaultValue_ReturnsNewInstance()
    {
        // Arrange
        var original = new StackVariable("VAR", "original");

        // Act
        var modified = original.WithDefaultValue("modified");

        // Assert
        modified.DefaultValue.Should().Be("modified");
        original.DefaultValue.Should().Be("original"); // Unchanged
        modified.Should().NotBeSameAs(original);
    }

    [Fact]
    public void WithDefaultValue_PreservesOtherProperties()
    {
        // Arrange
        var original = new StackVariable(
            name: "VAR",
            defaultValue: "original",
            description: "Test variable",
            type: VariableType.Port,
            label: "Test Label",
            min: 1,
            max: 100);

        // Act
        var modified = original.WithDefaultValue("modified");

        // Assert
        modified.Name.Should().Be(original.Name);
        modified.Description.Should().Be(original.Description);
        modified.Type.Should().Be(original.Type);
        modified.Label.Should().Be(original.Label);
        modified.Min.Should().Be(original.Min);
        modified.Max.Should().Be(original.Max);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_RequiredWithValue_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable("REQUIRED", null, null, VariableType.String, isRequired: true);

        // Act
        var result = variable.Validate("some-value");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RequiredWithoutValue_ReturnsInvalid()
    {
        // Arrange
        var variable = new StackVariable("REQUIRED", null, null, VariableType.String, isRequired: true);

        // Act
        var result = variable.Validate(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("required"));
    }

    [Fact]
    public void Validate_RequiredWithEmptyValue_ReturnsInvalid()
    {
        // Arrange
        var variable = new StackVariable("REQUIRED", null, null, VariableType.String, isRequired: true);

        // Act
        var result = variable.Validate("");

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_PatternMatch_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "EMAIL",
            defaultValue: null,
            description: null,
            type: VariableType.String,
            pattern: @"^[\w\.-]+@[\w\.-]+\.\w+$");

        // Act
        var result = variable.Validate("test@example.com");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PatternNoMatch_ReturnsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "EMAIL",
            defaultValue: null,
            description: null,
            type: VariableType.String,
            pattern: @"^[\w\.-]+@[\w\.-]+\.\w+$",
            patternError: "Invalid email format");

        // Act
        var result = variable.Validate("not-an-email");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Invalid email format");
    }

    [Fact]
    public void Validate_NumberInRange_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "PORT",
            defaultValue: null,
            description: null,
            type: VariableType.Port,
            min: 1024,
            max: 65535);

        // Act
        var result = variable.Validate("8080");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NumberBelowMin_ReturnsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "PORT",
            defaultValue: null,
            description: null,
            type: VariableType.Port,
            min: 1024,
            max: 65535);

        // Act
        var result = variable.Validate("80");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("1024"));
    }

    [Fact]
    public void Validate_NumberAboveMax_ReturnsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "PORT",
            defaultValue: null,
            description: null,
            type: VariableType.Port,
            min: 1024,
            max: 65535);

        // Act
        var result = variable.Validate("70000");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("65535"));
    }

    [Fact]
    public void Validate_SelectValidOption_ReturnsValid()
    {
        // Arrange
        var options = new[]
        {
            new SelectOption("dev", "Development"),
            new SelectOption("prod", "Production")
        };
        var variable = new StackVariable(
            name: "ENV",
            defaultValue: null,
            description: null,
            type: VariableType.Select,
            options: options);

        // Act
        var result = variable.Validate("dev");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SelectInvalidOption_ReturnsInvalid()
    {
        // Arrange
        var options = new[]
        {
            new SelectOption("dev", "Development"),
            new SelectOption("prod", "Production")
        };
        var variable = new StackVariable(
            name: "ENV",
            defaultValue: null,
            description: null,
            type: VariableType.Select,
            options: options);

        // Act
        var result = variable.Validate("staging");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("one of:"));
    }

    #endregion

    #region SelectOption Tests

    [Fact]
    public void SelectOption_Create_CreatesCorrectly()
    {
        // Act
        var option = new SelectOption("value", "Label", "Description");

        // Assert
        option.Value.Should().Be("value");
        option.Label.Should().Be("Label");
        option.Description.Should().Be("Description");
    }

    [Fact]
    public void SelectOption_WithoutLabelAndDescription_UsesValue()
    {
        // Act
        var option = new SelectOption("value");

        // Assert
        option.Value.Should().Be("value");
        option.Label.Should().BeNull();
        option.Description.Should().BeNull();
    }

    [Fact]
    public void SelectOption_Equality_WorksCorrectly()
    {
        // Arrange
        var o1 = new SelectOption("value", "Label");
        var o2 = new SelectOption("value", "Label");

        // Assert
        o1.Should().Be(o2);
    }

    #endregion

    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_Valid_HasNoErrors()
    {
        // Act
        var result = new ValidationResult(true, Array.Empty<string>());

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_Invalid_HasErrors()
    {
        // Act
        var result = new ValidationResult(false, new[] { "Error 1", "Error 2" });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    #endregion
}
