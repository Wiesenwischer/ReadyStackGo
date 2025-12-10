using FluentAssertions;
using ReadyStackGo.Domain.Catalog.Manifests;
using ReadyStackGo.Domain.Catalog.Stacks;

namespace ReadyStackGo.UnitTests.Domain.Catalog;

/// <summary>
/// Unit tests for StackVariableResolver domain service.
/// </summary>
public class StackVariableResolverTests
{
    private readonly StackVariableResolver _sut;

    public StackVariableResolverTests()
    {
        _sut = new StackVariableResolver();
    }

    #region Resolve - Success Tests

    [Fact]
    public void Resolve_WithAllVariablesProvided_ReturnsResolvedContent()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  db:\n    image: mysql:${MYSQL_VERSION}",
            new StackVariable("MYSQL_VERSION"));
        var values = new Dictionary<string, string> { { "MYSQL_VERSION", "8.0" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedContent.Should().Contain("image: mysql:8.0");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WithDefaultValues_UsesDefaultsWhenNotProvided()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  web:\n    image: nginx:${NGINX_VERSION}",
            new StackVariable("NGINX_VERSION", "latest"));
        var values = new Dictionary<string, string>();

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedContent.Should().Contain("image: nginx:latest");
    }

    [Fact]
    public void Resolve_WithProvidedOverridingDefault_UsesProvidedValue()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  web:\n    image: nginx:${NGINX_VERSION}",
            new StackVariable("NGINX_VERSION", "latest"));
        var values = new Dictionary<string, string> { { "NGINX_VERSION", "1.21" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedContent.Should().Contain("image: nginx:1.21");
    }

    [Fact]
    public void Resolve_WithMultipleVariables_ResolvesAll()
    {
        // Arrange
        var yaml = @"services:
  db:
    image: ${DB_IMAGE}:${DB_VERSION}
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_PASSWORD}";
        var stack = CreateStackWithVariables(yaml,
            new StackVariable("DB_IMAGE"),
            new StackVariable("DB_VERSION", "8.0"),
            new StackVariable("DB_PASSWORD"));
        var values = new Dictionary<string, string>
        {
            { "DB_IMAGE", "mysql" },
            { "DB_PASSWORD", "secret123" }
        };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedContent.Should().Contain("image: mysql:8.0");
        result.ResolvedContent.Should().Contain("MYSQL_ROOT_PASSWORD: secret123");
    }

    [Fact]
    public void Resolve_WithInlineDefaults_UsesInlineDefaultWhenNoValue()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  web:\n    port: ${PORT:-8080}");
        var values = new Dictionary<string, string>();

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedContent.Should().Contain("port: 8080");
    }

    [Fact]
    public void Resolve_WithDollarSignVariable_ResolvesSimpleFormat()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  web:\n    image: nginx:$NGINX_VERSION",
            new StackVariable("NGINX_VERSION"));
        var values = new Dictionary<string, string> { { "NGINX_VERSION", "1.21" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedContent.Should().Contain("image: nginx:1.21");
    }

    #endregion

    #region Resolve - Error Tests

    [Fact]
    public void Resolve_WithMissingRequiredVariable_ReturnsError()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  db:\n    password: ${DB_PASSWORD}",
            new StackVariable("DB_PASSWORD")); // Required (no default)
        var values = new Dictionary<string, string>();

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Type == VariableResolutionErrorType.RequiredVariableMissing &&
            e.VariableName == "DB_PASSWORD");
    }

    [Fact]
    public void Resolve_WithEmptyRequiredVariable_ReturnsError()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  db:\n    password: ${DB_PASSWORD}",
            new StackVariable("DB_PASSWORD"));
        var values = new Dictionary<string, string> { { "DB_PASSWORD", "" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Type == VariableResolutionErrorType.RequiredVariableMissing);
    }

    [Fact]
    public void Resolve_WithInvalidPortValue_ReturnsValidationError()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  web:\n    port: ${PORT}",
            new StackVariable("PORT", null, "Port number", VariableType.Port));
        var values = new Dictionary<string, string> { { "PORT", "invalid" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Type == VariableResolutionErrorType.ValidationFailed &&
            e.VariableName == "PORT");
    }

    [Fact]
    public void Resolve_WithOutOfRangePort_ReturnsValidationError()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  web:\n    port: ${PORT}",
            new StackVariable("PORT", null, "Port number", VariableType.Port));
        var values = new Dictionary<string, string> { { "PORT", "99999" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Type == VariableResolutionErrorType.ValidationFailed);
    }

    [Fact]
    public void Resolve_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "services:\n  db:\n    password: ${DB_PASSWORD}\n    port: ${DB_PORT}",
            new StackVariable("DB_PASSWORD"),
            new StackVariable("DB_PORT", null, "Port", VariableType.Port));
        var values = new Dictionary<string, string> { { "DB_PORT", "invalid" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }

    #endregion

    #region Resolve - Additional Files Tests

    [Fact]
    public void Resolve_WithAdditionalFiles_ResolvesVariablesInAll()
    {
        // Arrange
        var stack = new StackDefinition(
            sourceId: "local",
            name: "test",
            yamlContent: "services:\n  web:\n    image: ${IMAGE}",
            variables: new[] { new StackVariable("IMAGE", "nginx") },
            services: new[] { "web" },
            additionalFileContents: new Dictionary<string, string>
            {
                { "override.yml", "services:\n  web:\n    environment:\n      - VERSION=${IMAGE}" }
            });
        var values = new Dictionary<string, string> { { "IMAGE", "myapp" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedContent.Should().Contain("image: myapp");
        result.ResolvedAdditionalFiles.Should().ContainKey("override.yml");
        result.ResolvedAdditionalFiles!["override.yml"].Should().Contain("VERSION=myapp");
    }

    #endregion

    #region ExtractVariables Tests

    [Fact]
    public void ExtractVariables_WithBraceSyntax_ExtractsVariables()
    {
        // Arrange
        var yaml = "services:\n  db:\n    image: ${DB_IMAGE}:${DB_VERSION}";

        // Act
        var variables = _sut.ExtractVariables(yaml).ToList();

        // Assert
        variables.Should().HaveCount(2);
        variables.Should().Contain(v => v.Name == "DB_IMAGE");
        variables.Should().Contain(v => v.Name == "DB_VERSION");
    }

    [Fact]
    public void ExtractVariables_WithDollarSignSyntax_ExtractsVariables()
    {
        // Arrange
        var yaml = "services:\n  web:\n    image: nginx:$NGINX_VERSION";

        // Act
        var variables = _sut.ExtractVariables(yaml).ToList();

        // Assert
        variables.Should().ContainSingle(v => v.Name == "NGINX_VERSION");
    }

    [Fact]
    public void ExtractVariables_WithInlineDefaults_ExtractsDefaultValue()
    {
        // Arrange
        var yaml = "services:\n  web:\n    port: ${PORT:-8080}";

        // Act
        var variables = _sut.ExtractVariables(yaml).ToList();

        // Assert
        variables.Should().ContainSingle();
        variables[0].Name.Should().Be("PORT");
        variables[0].InlineDefault.Should().Be("8080");
    }

    [Fact]
    public void ExtractVariables_WithDuplicates_ReturnsUnique()
    {
        // Arrange
        var yaml = "services:\n  db:\n    image: ${DB_IMAGE}\n  adminer:\n    depends_on: ${DB_IMAGE}";

        // Act
        var variables = _sut.ExtractVariables(yaml).ToList();

        // Assert
        variables.Should().ContainSingle(v => v.Name == "DB_IMAGE");
    }

    [Fact]
    public void ExtractVariables_WithEmptyContent_ReturnsEmpty()
    {
        // Act
        var variables = _sut.ExtractVariables("").ToList();

        // Assert
        variables.Should().BeEmpty();
    }

    [Fact]
    public void ExtractVariables_WithNoVariables_ReturnsEmpty()
    {
        // Arrange
        var yaml = "services:\n  web:\n    image: nginx:latest";

        // Act
        var variables = _sut.ExtractVariables(yaml).ToList();

        // Assert
        variables.Should().BeEmpty();
    }

    #endregion

    #region Preview Tests

    [Fact]
    public void Preview_ShowsProvidedValues()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "test",
            new StackVariable("VAR1"),
            new StackVariable("VAR2", "default2"));
        var values = new Dictionary<string, string>
        {
            { "VAR1", "provided1" },
            { "VAR2", "provided2" }
        };

        // Act
        var preview = _sut.Preview(stack, values);

        // Assert
        preview.Items.Should().HaveCount(2);
        preview.GetProvided().Should().HaveCount(2);
        preview.Items.Single(i => i.Name == "VAR1").EffectiveValue.Should().Be("provided1");
        preview.Items.Single(i => i.Name == "VAR2").EffectiveValue.Should().Be("provided2");
    }

    [Fact]
    public void Preview_ShowsDefaultValues()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "test",
            new StackVariable("VAR1", "default1"));
        var values = new Dictionary<string, string>();

        // Act
        var preview = _sut.Preview(stack, values);

        // Assert
        preview.GetUsingDefaults().Should().ContainSingle();
        preview.Items[0].ValueSource.Should().Be(VariableValueSource.Default);
        preview.Items[0].EffectiveValue.Should().Be("default1");
    }

    [Fact]
    public void Preview_ShowsMissingRequiredVariables()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "test",
            new StackVariable("REQUIRED_VAR"));
        var values = new Dictionary<string, string>();

        // Act
        var preview = _sut.Preview(stack, values);

        // Assert
        preview.GetMissing().Should().ContainSingle();
        preview.Items[0].ValueSource.Should().Be(VariableValueSource.Missing);
        preview.Items[0].IsRequired.Should().BeTrue();
    }

    #endregion

    #region AreAllRequiredVariablesProvided Tests

    [Fact]
    public void AreAllRequiredVariablesProvided_AllProvided_ReturnsTrue()
    {
        // Arrange
        var stack = CreateStackWithVariables("test",
            new StackVariable("VAR1"),
            new StackVariable("VAR2"));
        var values = new Dictionary<string, string>
        {
            { "VAR1", "value1" },
            { "VAR2", "value2" }
        };

        // Act
        var result = _sut.AreAllRequiredVariablesProvided(stack, values);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreAllRequiredVariablesProvided_MissingRequired_ReturnsFalse()
    {
        // Arrange
        var stack = CreateStackWithVariables("test",
            new StackVariable("VAR1"),
            new StackVariable("VAR2"));
        var values = new Dictionary<string, string> { { "VAR1", "value1" } };

        // Act
        var result = _sut.AreAllRequiredVariablesProvided(stack, values);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AreAllRequiredVariablesProvided_OnlyOptional_ReturnsTrue()
    {
        // Arrange
        var stack = CreateStackWithVariables("test",
            new StackVariable("OPT1", "default1"),
            new StackVariable("OPT2", "default2"));
        var values = new Dictionary<string, string>();

        // Act
        var result = _sut.AreAllRequiredVariablesProvided(stack, values);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region VariableResolutionResult Tests

    [Fact]
    public void VariableResolutionResult_TracksResolvedVariables()
    {
        // Arrange
        var stack = CreateStackWithVariables(
            "test: ${VAR1}",
            new StackVariable("VAR1"));
        var values = new Dictionary<string, string> { { "VAR1", "resolved" } };

        // Act
        var result = _sut.Resolve(stack, values);

        // Assert
        result.ResolvedVariables.Should().ContainKey("VAR1");
        result.ResolvedVariables["VAR1"].Should().Be("resolved");
    }

    #endregion

    #region Helper Methods

    private static StackDefinition CreateStackWithVariables(string yaml, params StackVariable[] variables)
    {
        return new StackDefinition(
            sourceId: "local",
            name: "test-stack",
            yamlContent: yaml,
            variables: variables,
            services: new[] { "web" });
    }

    #endregion
}
