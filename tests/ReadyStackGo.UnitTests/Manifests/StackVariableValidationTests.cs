using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;

namespace ReadyStackGo.UnitTests.Manifests;

public class StackVariableValidationTests
{
    #region String Validation

    [Fact]
    public void Validate_RequiredStringMissing_ReturnsInvalid()
    {
        // Arrange
        var variable = new StackVariable("NAME", null, "User name", VariableType.String, isRequired: true);

        // Act
        var result = variable.Validate(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("required"));
    }

    [Fact]
    public void Validate_RequiredStringProvided_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable("NAME", null, "User name", VariableType.String, isRequired: true);

        // Act
        var result = variable.Validate("John");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OptionalStringEmpty_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable("NAME", "default", "User name", VariableType.String);

        // Act
        var result = variable.Validate("");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StringWithPattern_Valid_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable(
            "EMAIL", null, "Email address", VariableType.String,
            pattern: @"^[^@]+@[^@]+\.[^@]+$",
            patternError: "Must be a valid email");

        // Act
        var result = variable.Validate("test@example.com");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StringWithPattern_Invalid_ReturnsError()
    {
        // Arrange
        var variable = new StackVariable(
            "EMAIL", null, "Email address", VariableType.String,
            pattern: @"^[^@]+@[^@]+\.[^@]+$",
            patternError: "Must be a valid email");

        // Act
        var result = variable.Validate("not-an-email");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("valid email"));
    }

    [Fact]
    public void Validate_StringWithInvalidPattern_SkipsValidation()
    {
        // Arrange
        var variable = new StackVariable(
            "TEST", null, "Test", VariableType.String,
            pattern: "[invalid(regex");

        // Act
        var result = variable.Validate("anyvalue");

        // Assert
        result.IsValid.Should().BeTrue(); // Invalid regex is skipped, not an error
    }

    #endregion

    #region Number Validation

    [Fact]
    public void Validate_Number_Valid_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable("COUNT", null, "Count", VariableType.Number);

        // Act
        var result = variable.Validate("42");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Number_InvalidFormat_ReturnsError()
    {
        // Arrange
        var variable = new StackVariable("COUNT", null, "Count", VariableType.Number);

        // Act
        var result = variable.Validate("not-a-number");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("valid number"));
    }

    [Fact]
    public void Validate_Number_BelowMin_ReturnsError()
    {
        // Arrange
        var variable = new StackVariable(
            "COUNT", null, "Count", VariableType.Number,
            min: 1, max: 100);

        // Act
        var result = variable.Validate("0");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("at least 1"));
    }

    [Fact]
    public void Validate_Number_AboveMax_ReturnsError()
    {
        // Arrange
        var variable = new StackVariable(
            "COUNT", null, "Count", VariableType.Number,
            min: 1, max: 100);

        // Act
        var result = variable.Validate("150");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("at most 100"));
    }

    [Fact]
    public void Validate_Number_WithinRange_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable(
            "COUNT", null, "Count", VariableType.Number,
            min: 1, max: 100);

        // Act
        var result = variable.Validate("50");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Number_Decimal_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable("RATE", null, "Rate", VariableType.Number);

        // Act
        var result = variable.Validate("3.14");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Port Validation

    [Fact]
    public void Validate_Port_Valid_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable("PORT", null, "Port", VariableType.Port);

        // Act
        var result = variable.Validate("8080");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Port_TooLow_ReturnsError()
    {
        // Arrange
        var variable = new StackVariable("PORT", null, "Port", VariableType.Port);

        // Act
        var result = variable.Validate("0");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("1-65535"));
    }

    [Fact]
    public void Validate_Port_TooHigh_ReturnsError()
    {
        // Arrange
        var variable = new StackVariable("PORT", null, "Port", VariableType.Port);

        // Act
        var result = variable.Validate("70000");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("1-65535"));
    }

    [Fact]
    public void Validate_Port_Boundaries_ReturnsValid()
    {
        // Arrange
        var variable = new StackVariable("PORT", null, "Port", VariableType.Port);

        // Act & Assert
        variable.Validate("1").IsValid.Should().BeTrue();
        variable.Validate("65535").IsValid.Should().BeTrue();
    }

    #endregion

    #region Boolean Validation

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("True")]
    [InlineData("False")]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    [InlineData("1")]
    [InlineData("0")]
    public void Validate_Boolean_ValidValues_ReturnsValid(string value)
    {
        // Arrange
        var variable = new StackVariable("ENABLED", null, "Enabled", VariableType.Boolean);

        // Act
        var result = variable.Validate(value);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("on")]
    [InlineData("off")]
    [InlineData("maybe")]
    public void Validate_Boolean_InvalidValues_ReturnsError(string value)
    {
        // Arrange
        var variable = new StackVariable("ENABLED", null, "Enabled", VariableType.Boolean);

        // Act
        var result = variable.Validate(value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("true or false"));
    }

    #endregion

    #region Select Validation

    [Fact]
    public void Validate_Select_ValidOption_ReturnsValid()
    {
        // Arrange
        var options = new[]
        {
            new SelectOption("mysql", "MySQL"),
            new SelectOption("postgres", "PostgreSQL"),
            new SelectOption("sqlite", "SQLite")
        };
        var variable = new StackVariable(
            "DB_TYPE", null, "Database Type", VariableType.Select,
            options: options);

        // Act
        var result = variable.Validate("postgres");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Select_InvalidOption_ReturnsError()
    {
        // Arrange
        var options = new[]
        {
            new SelectOption("mysql", "MySQL"),
            new SelectOption("postgres", "PostgreSQL")
        };
        var variable = new StackVariable(
            "DB_TYPE", null, "Database Type", VariableType.Select,
            options: options);

        // Act
        var result = variable.Validate("oracle");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("mysql") && e.Contains("postgres"));
    }

    [Fact]
    public void Validate_Select_EmptyOptions_ReturnsValid()
    {
        // Arrange (no options defined - skip validation)
        var variable = new StackVariable(
            "TYPE", null, "Type", VariableType.Select);

        // Act
        var result = variable.Validate("anything");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Password Validation

    [Fact]
    public void Validate_Password_WithPattern_ValidatesPattern()
    {
        // Arrange
        var variable = new StackVariable(
            "PASSWORD", null, "Password", VariableType.Password,
            pattern: @"^.{8,}$",
            patternError: "Password must be at least 8 characters");

        // Act
        var result = variable.Validate("short");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("8 characters"));
    }

    [Fact]
    public void Validate_Password_WithoutPattern_AcceptsAny()
    {
        // Arrange
        var variable = new StackVariable("PASSWORD", null, "Password", VariableType.Password);

        // Act
        var result = variable.Validate("x");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Label and Error Message Tests

    [Fact]
    public void Validate_UsesLabelInErrorMessages()
    {
        // Arrange
        var variable = new StackVariable(
            "DB_PORT", null, "Database Port", VariableType.Port,
            label: "Database Port",
            isRequired: true);

        // Act
        var result = variable.Validate(null);

        // Assert
        result.Errors.Should().ContainSingle(e => e.Contains("Database Port"));
    }

    [Fact]
    public void Validate_FallsBackToNameIfNoLabel()
    {
        // Arrange
        var variable = new StackVariable("DB_PORT", null, "Port", VariableType.Port, isRequired: true);

        // Act
        var result = variable.Validate(null);

        // Assert
        result.Errors.Should().ContainSingle(e => e.Contains("DB_PORT"));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_SimpleForm_SetsDefaults()
    {
        // Arrange & Act
        var variable = new StackVariable("TEST", "default", "Description");

        // Assert
        variable.Type.Should().Be(VariableType.String);
        variable.IsRequired.Should().BeFalse(); // has default
        variable.DefaultValue.Should().Be("default");
    }

    [Fact]
    public void Constructor_NullDefault_SetsRequired()
    {
        // Arrange & Act
        var variable = new StackVariable("TEST", null, "Description");

        // Assert
        variable.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ExplicitRequired_OverridesDefault()
    {
        // Arrange & Act
        var variable = new StackVariable("TEST", "default", "Description", VariableType.String, isRequired: true);

        // Assert
        variable.IsRequired.Should().BeTrue();
        variable.DefaultValue.Should().Be("default");
    }

    [Fact]
    public void WithDefaultValue_CreatesNewInstance()
    {
        // Arrange
        var original = new StackVariable("TEST", null, "Description", VariableType.Number, min: 1, max: 100);

        // Act
        var modified = original.WithDefaultValue("50");

        // Assert
        original.DefaultValue.Should().BeNull();
        modified.DefaultValue.Should().Be("50");
        modified.Min.Should().Be(1);
        modified.Max.Should().Be(100);
    }

    #endregion
}
