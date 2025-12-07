using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;

namespace ReadyStackGo.UnitTests.Stacks;

/// <summary>
/// Tests for variable handling in RSGo Manifest Format.
/// Variables are now defined in the manifest with type information,
/// validation rules, and explicit required flags.
/// </summary>
public class VariablePriorityTests
{
    #region StackVariable Creation Tests

    [Fact]
    public void StackVariable_WithDefault_IsNotRequired()
    {
        // Arrange & Act
        var variable = new StackVariable("VERSION", "1.0.0", "Application version");

        // Assert
        variable.IsRequired.Should().BeFalse(
            because: "Variables with default values should not be required");
        variable.DefaultValue.Should().Be("1.0.0");
    }

    [Fact]
    public void StackVariable_WithoutDefault_IsRequired()
    {
        // Arrange & Act
        var variable = new StackVariable("API_KEY", null, "API Key");

        // Assert
        variable.IsRequired.Should().BeTrue(
            because: "Variables without default values should be required");
        variable.DefaultValue.Should().BeNull();
    }

    [Fact]
    public void StackVariable_ExplicitlyRequired_StaysRequired()
    {
        // Arrange & Act - Variable with default but explicitly required
        var variable = new StackVariable(
            name: "DB_PASSWORD",
            defaultValue: "changeme",
            description: "Database password",
            type: VariableType.Password,
            isRequired: true);

        // Assert
        variable.IsRequired.Should().BeTrue(
            because: "Explicitly required variables should stay required even with defaults");
        variable.DefaultValue.Should().Be("changeme");
    }

    [Fact]
    public void StackVariable_ExplicitlyOptional_StaysOptional()
    {
        // Arrange & Act - Variable without default but explicitly optional
        var variable = new StackVariable(
            name: "LOG_LEVEL",
            defaultValue: null,
            description: "Log level",
            type: VariableType.String,
            isRequired: false);

        // Assert
        variable.IsRequired.Should().BeFalse(
            because: "Explicitly optional variables should stay optional even without defaults");
    }

    #endregion

    #region StackVariable Type Tests

    [Fact]
    public void StackVariable_StringType_DefaultsCorrectly()
    {
        // Arrange & Act
        var variable = new StackVariable("NAME", "value", "Description");

        // Assert
        variable.Type.Should().Be(VariableType.String);
    }

    [Fact]
    public void StackVariable_PasswordType_PreservesType()
    {
        // Arrange & Act
        var variable = new StackVariable(
            name: "SECRET",
            defaultValue: null,
            description: "Secret value",
            type: VariableType.Password);

        // Assert
        variable.Type.Should().Be(VariableType.Password);
    }

    [Fact]
    public void StackVariable_PortType_PreservesType()
    {
        // Arrange & Act
        var variable = new StackVariable(
            name: "PORT",
            defaultValue: "8080",
            description: "Server port",
            type: VariableType.Port);

        // Assert
        variable.Type.Should().Be(VariableType.Port);
    }

    [Fact]
    public void StackVariable_SelectType_HasOptions()
    {
        // Arrange & Act
        var options = new List<SelectOption>
        {
            new("debug", "Debug", "Verbose logging"),
            new("info", "Info", "Standard logging"),
            new("warn", "Warning", "Warnings only"),
            new("error", "Error", "Errors only")
        };

        var variable = new StackVariable(
            name: "LOG_LEVEL",
            defaultValue: "info",
            description: "Logging level",
            type: VariableType.Select,
            options: options);

        // Assert
        variable.Type.Should().Be(VariableType.Select);
        variable.Options.Should().HaveCount(4);
        variable.Options.Should().Contain(o => o.Value == "info");
    }

    [Fact]
    public void StackVariable_NumberType_HasMinMax()
    {
        // Arrange & Act
        var variable = new StackVariable(
            name: "REPLICAS",
            defaultValue: "3",
            description: "Number of replicas",
            type: VariableType.Number,
            min: 1,
            max: 10);

        // Assert
        variable.Type.Should().Be(VariableType.Number);
        variable.Min.Should().Be(1);
        variable.Max.Should().Be(10);
    }

    #endregion

    #region StackVariable Validation Tests

    [Fact]
    public void Validate_RequiredWithValue_IsValid()
    {
        // Arrange
        var variable = new StackVariable("API_KEY", null, "API Key", VariableType.String, isRequired: true);

        // Act
        var result = variable.Validate("my-api-key");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RequiredWithoutValue_IsInvalid()
    {
        // Arrange
        var variable = new StackVariable("API_KEY", null, "API Key", VariableType.String, isRequired: true);

        // Act
        var result = variable.Validate(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("required");
    }

    [Fact]
    public void Validate_OptionalWithoutValue_IsValid()
    {
        // Arrange
        var variable = new StackVariable("OPTIONAL", "default", "Optional field");

        // Act
        var result = variable.Validate(null);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NumberType_ValidNumber_IsValid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "PORT",
            defaultValue: "8080",
            description: "Port",
            type: VariableType.Number);

        // Act
        var result = variable.Validate("3000");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NumberType_InvalidNumber_IsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "PORT",
            defaultValue: "8080",
            description: "Port",
            type: VariableType.Number);

        // Act
        var result = variable.Validate("not-a-number");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("valid number");
    }

    [Fact]
    public void Validate_NumberType_BelowMin_IsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "REPLICAS",
            defaultValue: "3",
            description: "Replicas",
            type: VariableType.Number,
            min: 1,
            max: 10);

        // Act
        var result = variable.Validate("0");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("at least");
    }

    [Fact]
    public void Validate_NumberType_AboveMax_IsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "REPLICAS",
            defaultValue: "3",
            description: "Replicas",
            type: VariableType.Number,
            min: 1,
            max: 10);

        // Act
        var result = variable.Validate("15");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("at most");
    }

    [Fact]
    public void Validate_PortType_ValidPort_IsValid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "PORT",
            defaultValue: "8080",
            description: "Port",
            type: VariableType.Port);

        // Act
        var result = variable.Validate("443");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PortType_InvalidPort_IsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "PORT",
            defaultValue: "8080",
            description: "Port",
            type: VariableType.Port);

        // Act
        var result = variable.Validate("70000");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("valid port");
    }

    [Fact]
    public void Validate_BooleanType_True_IsValid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "ENABLED",
            defaultValue: "true",
            description: "Enable feature",
            type: VariableType.Boolean);

        // Act
        var result = variable.Validate("true");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BooleanType_False_IsValid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "ENABLED",
            defaultValue: "true",
            description: "Enable feature",
            type: VariableType.Boolean);

        // Act
        var result = variable.Validate("false");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BooleanType_InvalidValue_IsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "ENABLED",
            defaultValue: "true",
            description: "Enable feature",
            type: VariableType.Boolean);

        // Act
        var result = variable.Validate("maybe");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("true or false");
    }

    [Fact]
    public void Validate_SelectType_ValidOption_IsValid()
    {
        // Arrange
        var options = new List<SelectOption>
        {
            new("debug"),
            new("info"),
            new("warn"),
            new("error")
        };

        var variable = new StackVariable(
            name: "LOG_LEVEL",
            defaultValue: "info",
            description: "Log level",
            type: VariableType.Select,
            options: options);

        // Act
        var result = variable.Validate("debug");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SelectType_InvalidOption_IsInvalid()
    {
        // Arrange
        var options = new List<SelectOption>
        {
            new("debug"),
            new("info"),
            new("warn"),
            new("error")
        };

        var variable = new StackVariable(
            name: "LOG_LEVEL",
            defaultValue: "info",
            description: "Log level",
            type: VariableType.Select,
            options: options);

        // Act
        var result = variable.Validate("verbose");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("must be one of");
    }

    [Fact]
    public void Validate_PatternValidation_MatchingPattern_IsValid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "VERSION",
            defaultValue: "1.0.0",
            description: "Semantic version",
            type: VariableType.String,
            pattern: @"^\d+\.\d+\.\d+$",
            patternError: "Must be a semantic version (e.g., 1.0.0)");

        // Act
        var result = variable.Validate("2.1.3");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PatternValidation_NonMatchingPattern_IsInvalid()
    {
        // Arrange
        var variable = new StackVariable(
            name: "VERSION",
            defaultValue: "1.0.0",
            description: "Semantic version",
            type: VariableType.String,
            pattern: @"^\d+\.\d+\.\d+$",
            patternError: "Must be a semantic version (e.g., 1.0.0)");

        // Act
        var result = variable.Validate("v1.0");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("semantic version");
    }

    #endregion

    #region StackVariable WithDefaultValue Tests

    [Fact]
    public void WithDefaultValue_CreatesNewInstanceWithNewDefault()
    {
        // Arrange
        var original = new StackVariable(
            name: "VERSION",
            defaultValue: "1.0.0",
            description: "Version",
            type: VariableType.String);

        // Act
        var updated = original.WithDefaultValue("2.0.0");

        // Assert
        updated.Should().NotBeSameAs(original);
        updated.DefaultValue.Should().Be("2.0.0");
        original.DefaultValue.Should().Be("1.0.0");
    }

    [Fact]
    public void WithDefaultValue_PreservesOtherProperties()
    {
        // Arrange
        var original = new StackVariable(
            name: "PORT",
            defaultValue: "8080",
            description: "Server port",
            type: VariableType.Port,
            label: "Port Number",
            min: 1,
            max: 65535,
            isRequired: true);

        // Act
        var updated = original.WithDefaultValue("3000");

        // Assert
        updated.Name.Should().Be("PORT");
        updated.Description.Should().Be("Server port");
        updated.Type.Should().Be(VariableType.Port);
        updated.Label.Should().Be("Port Number");
        updated.Min.Should().Be(1);
        updated.Max.Should().Be(65535);
        updated.IsRequired.Should().BeTrue();
    }

    #endregion

    #region StackDefinition Variable Helper Tests

    [Fact]
    public void GetRequiredVariables_ReturnsOnlyRequired()
    {
        // Arrange
        var variables = new List<StackVariable>
        {
            new("REQUIRED_1", null, "Required var 1", VariableType.String),
            new("OPTIONAL_1", "default", "Optional var 1", VariableType.String),
            new("REQUIRED_2", null, "Required var 2", VariableType.Password)
        };

        var stack = new StackDefinition(
            sourceId: "test",
            name: "TestStack",
            yamlContent: "test content",
            variables: variables);

        // Act
        var requiredVars = stack.GetRequiredVariables().ToList();

        // Assert
        requiredVars.Should().HaveCount(2);
        requiredVars.Should().Contain(v => v.Name == "REQUIRED_1");
        requiredVars.Should().Contain(v => v.Name == "REQUIRED_2");
        requiredVars.Should().NotContain(v => v.Name == "OPTIONAL_1");
    }

    [Fact]
    public void GetOptionalVariables_ReturnsOnlyOptional()
    {
        // Arrange
        var variables = new List<StackVariable>
        {
            new("REQUIRED_1", null, "Required var 1", VariableType.String),
            new("OPTIONAL_1", "default1", "Optional var 1", VariableType.String),
            new("OPTIONAL_2", "default2", "Optional var 2", VariableType.Number)
        };

        var stack = new StackDefinition(
            sourceId: "test",
            name: "TestStack",
            yamlContent: "test content",
            variables: variables);

        // Act
        var optionalVars = stack.GetOptionalVariables().ToList();

        // Assert
        optionalVars.Should().HaveCount(2);
        optionalVars.Should().Contain(v => v.Name == "OPTIONAL_1");
        optionalVars.Should().Contain(v => v.Name == "OPTIONAL_2");
        optionalVars.Should().NotContain(v => v.Name == "REQUIRED_1");
    }

    #endregion
}
