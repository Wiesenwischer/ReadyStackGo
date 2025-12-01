using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.StackManagement.Aggregates;
using ReadyStackGo.Domain.StackManagement.ValueObjects;
using ReadyStackGo.Infrastructure.Stacks.Sources;

namespace ReadyStackGo.UnitTests.Stacks;

/// <summary>
/// Tests for variable priority handling in stack sources.
///
/// Priority Order (highest to lowest):
/// 1. User input during deployment
/// 2. .env file value
/// 3. YAML inline default (${VAR:-default}) - fallback only
///
/// These tests verify that ReadyStackGo correctly implements Docker Compose semantics
/// where .env file values take precedence over YAML inline defaults.
/// </summary>
public class VariablePriorityTests : IDisposable
{
    private readonly Mock<ILogger<LocalDirectoryStackSourceProvider>> _loggerMock;
    private readonly LocalDirectoryStackSourceProvider _provider;
    private readonly string _tempDir;

    public VariablePriorityTests()
    {
        _loggerMock = new Mock<ILogger<LocalDirectoryStackSourceProvider>>();
        _provider = new LocalDirectoryStackSourceProvider(_loggerMock.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-priority-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region Priority 2 vs 3: .env vs YAML Default

    [Fact]
    public async Task Priority_EnvFileOverridesYamlDefault_EnvValueUsed()
    {
        // Arrange
        // YAML has default "yaml-default", .env has "env-value"
        // Expected: .env wins → "env-value"
        var stackFolder = CreateStackFolder("env-wins");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  app:
    image: nginx:${VERSION:-yaml-default}
");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, ".env"),
            "VERSION=env-value"
        );

        // Act
        var stacks = await LoadStacksAsync();

        // Assert
        var version = stacks.First().Variables.First(v => v.Name == "VERSION");
        version.DefaultValue.Should().Be("env-value",
            because: ".env value should override YAML default per Docker Compose semantics");
    }

    [Fact]
    public async Task Priority_EnvFileWithEmptyValue_OverridesYamlDefault()
    {
        // Arrange
        // YAML has default "yaml-default", .env has empty string
        // Expected: .env wins → "" (empty string is valid)
        var stackFolder = CreateStackFolder("env-empty");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  app:
    image: nginx:${VERSION:-yaml-default}
");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, ".env"),
            "VERSION="
        );

        // Act
        var stacks = await LoadStacksAsync();

        // Assert
        var version = stacks.First().Variables.First(v => v.Name == "VERSION");
        version.DefaultValue.Should().Be("",
            because: "Empty .env value should still override YAML default");
    }

    [Fact]
    public async Task Priority_NoEnvFile_YamlDefaultUsed()
    {
        // Arrange
        // YAML has default "yaml-default", no .env file
        // Expected: YAML default → "yaml-default"
        var stackFolder = CreateStackFolder("yaml-fallback");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  app:
    image: nginx:${VERSION:-yaml-default}
");

        // No .env file created

        // Act
        var stacks = await LoadStacksAsync();

        // Assert
        var version = stacks.First().Variables.First(v => v.Name == "VERSION");
        version.DefaultValue.Should().Be("yaml-default",
            because: "Without .env, YAML default should be used as fallback");
    }

    [Fact]
    public async Task Priority_EnvFileMissingVariable_YamlDefaultUsed()
    {
        // Arrange
        // YAML has VERSION with default, .env has OTHER_VAR
        // Expected: VERSION uses YAML default, OTHER_VAR from .env
        var stackFolder = CreateStackFolder("partial-env");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  app:
    image: nginx:${VERSION:-yaml-default}
    environment:
      - OTHER=${OTHER_VAR}
");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, ".env"),
            "OTHER_VAR=from-env"
        );

        // Act
        var stacks = await LoadStacksAsync();

        // Assert
        var vars = stacks.First().Variables;

        var version = vars.First(v => v.Name == "VERSION");
        version.DefaultValue.Should().Be("yaml-default",
            because: "Variable not in .env should use YAML default");

        var other = vars.First(v => v.Name == "OTHER_VAR");
        other.DefaultValue.Should().Be("from-env",
            because: "Variable in .env should use .env value");
    }

    #endregion

    #region Required vs Optional Variables

    [Fact]
    public async Task Priority_RequiredVarBecomesOptionalWithEnv()
    {
        // Arrange
        // YAML has required variable (no default), .env provides value
        // Expected: Variable becomes optional (has default from .env)
        var stackFolder = CreateStackFolder("required-to-optional");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  db:
    image: mysql:8.0
    environment:
      - MYSQL_ROOT_PASSWORD=${DB_PASSWORD}
");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, ".env"),
            "DB_PASSWORD=secret123"
        );

        // Act
        var stacks = await LoadStacksAsync();

        // Assert
        var dbPassword = stacks.First().Variables.First(v => v.Name == "DB_PASSWORD");
        dbPassword.IsRequired.Should().BeFalse(
            because: ".env provides a default value, making it optional");
        dbPassword.DefaultValue.Should().Be("secret123");
    }

    [Fact]
    public async Task Priority_RequiredVarWithoutEnv_StaysRequired()
    {
        // Arrange
        // YAML has required variable (no default), no .env
        // Expected: Variable stays required
        var stackFolder = CreateStackFolder("stays-required");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  db:
    image: mysql:8.0
    environment:
      - MYSQL_ROOT_PASSWORD=${DB_PASSWORD}
");

        // No .env file

        // Act
        var stacks = await LoadStacksAsync();

        // Assert
        var dbPassword = stacks.First().Variables.First(v => v.Name == "DB_PASSWORD");
        dbPassword.IsRequired.Should().BeTrue(
            because: "No default in YAML or .env, so variable is required");
        dbPassword.DefaultValue.Should().BeNull();
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public async Task Priority_MultipleVariables_EachResolvedCorrectly()
    {
        // Arrange
        // Test multiple variables with different priority scenarios
        var stackFolder = CreateStackFolder("multi-var");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  app:
    image: app:${APP_VERSION:-v1.0}
    environment:
      - DATABASE_URL=${DATABASE_URL}
      - LOG_LEVEL=${LOG_LEVEL:-info}
      - PORT=${PORT:-3000}
      - API_KEY=${API_KEY}
");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, ".env"),
            @"
# Override YAML default
LOG_LEVEL=debug
# Provide value for required var
API_KEY=secret-key
# PORT not in .env → should use YAML default
"
        );

        // Act
        var stacks = await LoadStacksAsync();
        var vars = stacks.First().Variables.ToDictionary(v => v.Name);

        // Assert

        // APP_VERSION: YAML default only → "v1.0"
        vars["APP_VERSION"].DefaultValue.Should().Be("v1.0");

        // DATABASE_URL: Required, not in .env → required, no default
        vars["DATABASE_URL"].IsRequired.Should().BeTrue();
        vars["DATABASE_URL"].DefaultValue.Should().BeNull();

        // LOG_LEVEL: YAML default "info", .env "debug" → "debug"
        vars["LOG_LEVEL"].DefaultValue.Should().Be("debug");

        // PORT: YAML default "3000", not in .env → "3000"
        vars["PORT"].DefaultValue.Should().Be("3000");

        // API_KEY: Required in YAML, .env provides value → optional "secret-key"
        vars["API_KEY"].IsRequired.Should().BeFalse();
        vars["API_KEY"].DefaultValue.Should().Be("secret-key");
    }

    [Fact]
    public async Task Priority_EnvOnlyVariables_IncludedAsInformational()
    {
        // Arrange
        // .env has variable not referenced in YAML
        // Expected: Variable shown as informational
        var stackFolder = CreateStackFolder("env-only");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  app:
    image: nginx:latest
");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, ".env"),
            "EXTRA_VAR=extra-value"
        );

        // Act
        var stacks = await LoadStacksAsync();

        // Assert
        var extraVar = stacks.First().Variables.FirstOrDefault(v => v.Name == "EXTRA_VAR");
        extraVar.Should().NotBeNull("Variables only in .env should be included");
        extraVar!.DefaultValue.Should().Be("extra-value");
        extraVar.IsRequired.Should().BeFalse();
    }

    #endregion

    #region .env Parsing Edge Cases

    [Fact]
    public async Task Priority_EnvWithQuotedValues_ParsedCorrectly()
    {
        // Arrange
        var stackFolder = CreateStackFolder("quoted-values");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  app:
    image: app:${VERSION}
    environment:
      - MSG1=${MSG1}
      - MSG2=${MSG2}
");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, ".env"),
            @"
VERSION=""1.2.3""
MSG1='Single quoted'
MSG2=""Double quoted with spaces""
"
        );

        // Act
        var stacks = await LoadStacksAsync();
        var vars = stacks.First().Variables.ToDictionary(v => v.Name);

        // Assert - quotes should be stripped
        vars["VERSION"].DefaultValue.Should().Be("1.2.3");
        vars["MSG1"].DefaultValue.Should().Be("Single quoted");
        vars["MSG2"].DefaultValue.Should().Be("Double quoted with spaces");
    }

    [Fact]
    public async Task Priority_EnvWithSpecialCharacters_ParsedCorrectly()
    {
        // Arrange
        var stackFolder = CreateStackFolder("special-chars");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, "docker-compose.yml"),
            @"
version: '3.8'
services:
  app:
    environment:
      - CONNECTION_STRING=${CONNECTION_STRING}
");

        await File.WriteAllTextAsync(
            Path.Combine(stackFolder, ".env"),
            "CONNECTION_STRING=postgres://user:pass@localhost:5432/db?sslmode=require"
        );

        // Act
        var stacks = await LoadStacksAsync();

        // Assert
        var connStr = stacks.First().Variables.First(v => v.Name == "CONNECTION_STRING");
        connStr.DefaultValue.Should().Be("postgres://user:pass@localhost:5432/db?sslmode=require");
    }

    #endregion

    #region Helper Methods

    private string CreateStackFolder(string name)
    {
        var folder = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private async Task<List<StackDefinition>> LoadStacksAsync()
    {
        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("test"),
            "Test",
            _tempDir,
            "*.yml;*.yaml");

        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);
        return stacks.ToList();
    }

    #endregion
}
