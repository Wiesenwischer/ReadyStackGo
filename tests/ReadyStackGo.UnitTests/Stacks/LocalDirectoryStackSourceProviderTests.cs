using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.StackManagement.Aggregates;
using ReadyStackGo.Domain.StackManagement.ValueObjects;
using ReadyStackGo.Infrastructure.Stacks.Sources;

namespace ReadyStackGo.UnitTests.Stacks;

public class LocalDirectoryStackSourceProviderTests : IDisposable
{
    private readonly Mock<ILogger<LocalDirectoryStackSourceProvider>> _loggerMock;
    private readonly LocalDirectoryStackSourceProvider _provider;
    private readonly string _tempDir;

    public LocalDirectoryStackSourceProviderTests()
    {
        _loggerMock = new Mock<ILogger<LocalDirectoryStackSourceProvider>>();
        _provider = new LocalDirectoryStackSourceProvider(_loggerMock.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region Single-File Stack Tests

    [Fact]
    public async Task LoadStacksAsync_SingleYamlFile_LoadsStack()
    {
        // Arrange
        var stackContent = @"
version: '3.8'
services:
  web:
    image: nginx:latest
    ports:
      - '8080:80'
";
        var stackFile = Path.Combine(_tempDir, "simple-nginx.yml");
        await File.WriteAllTextAsync(stackFile, stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("simple-nginx");
        stack.Services.Should().Contain("web");
    }

    [Fact]
    public async Task LoadStacksAsync_VariablesWithDefaults_DetectsVariables()
    {
        // Arrange
        var stackContent = @"
version: '3.8'
services:
  web:
    image: nginx:${NGINX_VERSION:-latest}
    ports:
      - '${PORT:-8080}:80'
";
        var stackFile = Path.Combine(_tempDir, "nginx-vars.yml");
        await File.WriteAllTextAsync(stackFile, stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.Variables.Should().HaveCount(2);
        stack.Variables.Should().Contain(v => v.Name == "NGINX_VERSION" && v.DefaultValue == "latest");
        stack.Variables.Should().Contain(v => v.Name == "PORT" && v.DefaultValue == "8080");
    }

    [Fact]
    public async Task LoadStacksAsync_RequiredVariables_MarkedAsRequired()
    {
        // Arrange
        var stackContent = @"
version: '3.8'
services:
  db:
    image: mysql:8.0
    environment:
      - MYSQL_ROOT_PASSWORD=${DB_PASSWORD}
      - MYSQL_DATABASE=${DB_NAME:-myapp}
";
        var stackFile = Path.Combine(_tempDir, "mysql.yml");
        await File.WriteAllTextAsync(stackFile, stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        var dbPassword = stack.Variables.First(v => v.Name == "DB_PASSWORD");
        dbPassword.IsRequired.Should().BeTrue();
        dbPassword.DefaultValue.Should().BeNull();

        var dbName = stack.Variables.First(v => v.Name == "DB_NAME");
        dbName.IsRequired.Should().BeFalse();
        dbName.DefaultValue.Should().Be("myapp");
    }

    #endregion

    #region Folder-Based Stack Tests

    [Fact]
    public async Task LoadStacksAsync_FolderBasedStack_LoadsFromFolder()
    {
        // Arrange
        var stackFolder = Path.Combine(_tempDir, "wordpress");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
version: '3.8'
services:
  wordpress:
    image: wordpress:latest
    ports:
      - '8080:80'
  db:
    image: mysql:8.0
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.yml"), composeContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("wordpress");
        stack.Services.Should().HaveCount(2);
        stack.Services.Should().Contain("wordpress");
        stack.Services.Should().Contain("db");
    }

    [Fact]
    public async Task LoadStacksAsync_WithEnvFile_AppliesEnvDefaults()
    {
        // Arrange
        var stackFolder = Path.Combine(_tempDir, "with-env");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
version: '3.8'
services:
  app:
    image: myapp:${APP_VERSION}
    environment:
      - DATABASE_URL=${DATABASE_URL}
      - PORT=${PORT:-3000}
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.yml"), composeContent);

        var envContent = @"
APP_VERSION=1.0.0
DATABASE_URL=postgres://localhost/app
PORT=8080
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, ".env"), envContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();

        var appVersion = stack.Variables.First(v => v.Name == "APP_VERSION");
        appVersion.DefaultValue.Should().Be("1.0.0");
        appVersion.IsRequired.Should().BeFalse();

        var dbUrl = stack.Variables.First(v => v.Name == "DATABASE_URL");
        dbUrl.DefaultValue.Should().Be("postgres://localhost/app");
        dbUrl.IsRequired.Should().BeFalse();

        // .env should override YAML default (8080 > 3000)
        var port = stack.Variables.First(v => v.Name == "PORT");
        port.DefaultValue.Should().Be("8080");
    }

    [Fact]
    public async Task LoadStacksAsync_EnvPriorityOverYamlDefault_EnvWins()
    {
        // Arrange
        var stackFolder = Path.Combine(_tempDir, "env-priority");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
version: '3.8'
services:
  app:
    image: nginx:${VERSION:-yaml-default}
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.yml"), composeContent);

        var envContent = "VERSION=env-value";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, ".env"), envContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        var version = stack.Variables.First(v => v.Name == "VERSION");

        // .env value should take precedence over YAML default
        version.DefaultValue.Should().Be("env-value");
    }

    [Fact]
    public async Task LoadStacksAsync_WithOverrideFile_IncludesInAdditionalFiles()
    {
        // Arrange
        var stackFolder = Path.Combine(_tempDir, "with-override");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
version: '3.8'
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.yml"), composeContent);

        var overrideContent = @"
version: '3.8'
services:
  web:
    ports:
      - '8080:80'
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.override.yml"), overrideContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.AdditionalFiles.Should().Contain("docker-compose.override.yml");
        stack.AdditionalFileContents.Should().ContainKey("docker-compose.override.yml");
    }

    [Fact]
    public async Task LoadStacksAsync_EnvFileWithQuotes_ParsesCorrectly()
    {
        // Arrange
        var stackFolder = Path.Combine(_tempDir, "quoted-env");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
version: '3.8'
services:
  app:
    image: app:${VERSION}
    environment:
      - MESSAGE=${MESSAGE}
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.yml"), composeContent);

        var envContent = @"
VERSION=""1.0.0""
MESSAGE='Hello World'
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, ".env"), envContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.Variables.First(v => v.Name == "VERSION").DefaultValue.Should().Be("1.0.0");
        stack.Variables.First(v => v.Name == "MESSAGE").DefaultValue.Should().Be("Hello World");
    }

    [Fact]
    public async Task LoadStacksAsync_EnvFileWithComments_IgnoresComments()
    {
        // Arrange
        var stackFolder = Path.Combine(_tempDir, "commented-env");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
version: '3.8'
services:
  app:
    image: app:${VERSION}
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.yml"), composeContent);

        var envContent = @"
# This is a comment
VERSION=1.0.0
# Another comment
# IGNORED_VAR=value
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, ".env"), envContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.Variables.Should().NotContain(v => v.Name == "IGNORED_VAR");
        stack.Variables.First(v => v.Name == "VERSION").DefaultValue.Should().Be("1.0.0");
    }

    #endregion

    #region Mixed Stack Tests

    [Fact]
    public async Task LoadStacksAsync_MixedSingleAndFolder_LoadsBoth()
    {
        // Arrange
        // Single file stack
        var singleContent = @"
version: '3.8'
services:
  redis:
    image: redis:alpine
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "redis.yml"), singleContent);

        // Folder-based stack
        var folderPath = Path.Combine(_tempDir, "postgres");
        Directory.CreateDirectory(folderPath);
        var folderContent = @"
version: '3.8'
services:
  db:
    image: postgres:15
";
        await File.WriteAllTextAsync(Path.Combine(folderPath, "docker-compose.yml"), folderContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(2);
        stacks.Should().Contain(s => s.Name == "redis");
        stacks.Should().Contain(s => s.Name == "postgres");
    }

    #endregion

    #region Recursive Search Tests

    [Fact]
    public async Task LoadStacksAsync_NestedFolderStack_LoadsWithRelativePath()
    {
        // Arrange - Create nested structure: stacks/examples/wordpress/docker-compose.yml
        var examplesDir = Path.Combine(_tempDir, "examples");
        var wordpressDir = Path.Combine(examplesDir, "wordpress");
        Directory.CreateDirectory(wordpressDir);

        var composeContent = @"
version: '3.8'
services:
  wordpress:
    image: wordpress:latest
";
        await File.WriteAllTextAsync(Path.Combine(wordpressDir, "docker-compose.yml"), composeContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("wordpress");
        stack.RelativePath.Should().Be("examples");
    }

    [Fact]
    public async Task LoadStacksAsync_DeeplyNestedFolderStack_LoadsWithFullRelativePath()
    {
        // Arrange - Create deeply nested: stacks/ams.project/identityaccess/docker-compose.yml
        var projectDir = Path.Combine(_tempDir, "ams.project");
        var identityDir = Path.Combine(projectDir, "identityaccess");
        Directory.CreateDirectory(identityDir);

        var composeContent = @"
version: '3.8'
services:
  identity-api:
    image: identity:latest
";
        await File.WriteAllTextAsync(Path.Combine(identityDir, "docker-compose.yml"), composeContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("identityaccess");
        stack.RelativePath.Should().Be(Path.Combine("ams.project"));
    }

    [Fact]
    public async Task LoadStacksAsync_FileBasedStackInSubdirectory_LoadsWithRelativePath()
    {
        // Arrange - Create: stacks/examples/simple-nginx.yml (file-based, not folder-based)
        var examplesDir = Path.Combine(_tempDir, "examples");
        Directory.CreateDirectory(examplesDir);

        var stackContent = @"
version: '3.8'
services:
  nginx:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(examplesDir, "simple-nginx.yml"), stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("simple-nginx");
        stack.RelativePath.Should().Be("examples");
    }

    [Fact]
    public async Task LoadStacksAsync_StackAtRootLevel_HasNullRelativePath()
    {
        // Arrange - Create: stacks/docker-compose.yml (directly in root)
        var stackFolder = Path.Combine(_tempDir, "myapp");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
version: '3.8'
services:
  app:
    image: app:latest
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.yml"), composeContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("myapp");
        stack.RelativePath.Should().BeNull();
    }

    [Fact]
    public async Task LoadStacksAsync_FileAtRootLevel_HasNullRelativePath()
    {
        // Arrange - Create: stacks/simple.yml (file directly in root)
        var stackContent = @"
version: '3.8'
services:
  simple:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "simple.yml"), stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("simple");
        stack.RelativePath.Should().BeNull();
    }

    [Fact]
    public async Task LoadStacksAsync_MixedNestedAndRootStacks_LoadsAllWithCorrectPaths()
    {
        // Arrange
        // Root level file: stacks/redis.yml
        var redisContent = @"
version: '3.8'
services:
  redis:
    image: redis:alpine
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "redis.yml"), redisContent);

        // Root level folder: stacks/postgres/docker-compose.yml
        var postgresDir = Path.Combine(_tempDir, "postgres");
        Directory.CreateDirectory(postgresDir);
        var postgresContent = @"
version: '3.8'
services:
  db:
    image: postgres:15
";
        await File.WriteAllTextAsync(Path.Combine(postgresDir, "docker-compose.yml"), postgresContent);

        // Nested folder: stacks/examples/wordpress/docker-compose.yml
        var examplesDir = Path.Combine(_tempDir, "examples");
        var wordpressDir = Path.Combine(examplesDir, "wordpress");
        Directory.CreateDirectory(wordpressDir);
        var wordpressContent = @"
version: '3.8'
services:
  wordpress:
    image: wordpress:latest
";
        await File.WriteAllTextAsync(Path.Combine(wordpressDir, "docker-compose.yml"), wordpressContent);

        // Nested file: stacks/examples/whoami.yml
        var whoamiContent = @"
version: '3.8'
services:
  whoami:
    image: traefik/whoami
";
        await File.WriteAllTextAsync(Path.Combine(examplesDir, "whoami.yml"), whoamiContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(4);

        var redis = stacks.First(s => s.Name == "redis");
        redis.RelativePath.Should().BeNull();

        var postgres = stacks.First(s => s.Name == "postgres");
        postgres.RelativePath.Should().BeNull();

        var wordpress = stacks.First(s => s.Name == "wordpress");
        wordpress.RelativePath.Should().Be("examples");

        var whoami = stacks.First(s => s.Name == "whoami");
        whoami.RelativePath.Should().Be("examples");
    }

    [Fact]
    public async Task LoadStacksAsync_MultipleNestedLevels_LoadsAllRecursively()
    {
        // Arrange - Create structure:
        // stacks/
        //   level1/
        //     stack1/docker-compose.yml
        //     level2/
        //       stack2/docker-compose.yml
        //       level3/
        //         stack3/docker-compose.yml

        var level1 = Path.Combine(_tempDir, "level1");
        var stack1 = Path.Combine(level1, "stack1");
        Directory.CreateDirectory(stack1);

        var level2 = Path.Combine(level1, "level2");
        var stack2 = Path.Combine(level2, "stack2");
        Directory.CreateDirectory(stack2);

        var level3 = Path.Combine(level2, "level3");
        var stack3 = Path.Combine(level3, "stack3");
        Directory.CreateDirectory(stack3);

        var composeTemplate = @"
version: '3.8'
services:
  app:
    image: app:latest
";
        await File.WriteAllTextAsync(Path.Combine(stack1, "docker-compose.yml"), composeTemplate);
        await File.WriteAllTextAsync(Path.Combine(stack2, "docker-compose.yml"), composeTemplate);
        await File.WriteAllTextAsync(Path.Combine(stack3, "docker-compose.yml"), composeTemplate);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(3);
        stacks.Should().Contain(s => s.Name == "stack1");
        stacks.Should().Contain(s => s.Name == "stack2");
        stacks.Should().Contain(s => s.Name == "stack3");

        var s1 = stacks.First(s => s.Name == "stack1");
        s1.RelativePath.Should().Be("level1");

        var s2 = stacks.First(s => s.Name == "stack2");
        s2.RelativePath.Should().Be(Path.Combine("level1", "level2"));

        var s3 = stacks.First(s => s.Name == "stack3");
        s3.RelativePath.Should().Be(Path.Combine("level1", "level2", "level3"));
    }

    #endregion

    #region Description Extraction Tests

    [Fact]
    public async Task LoadStacksAsync_MultiLineComments_ExtractsDescriptionWithNewlines()
    {
        // Arrange
        var stackContent = @"# My Stack Title
# This is a description
version: '3.8'
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "multiline.yml"), stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.Description.Should().Be("My Stack Title\nThis is a description");
    }

    [Fact]
    public async Task LoadStacksAsync_UsageLine_IsExcludedFromDescription()
    {
        // Arrange
        var stackContent = @"# Identity Provider
# Standalone deployment
# Usage: docker-compose up -d
version: '3.8'
services:
  app:
    image: identity:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "with-usage.yml"), stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.Description.Should().Be("Identity Provider\nStandalone deployment");
        stack.Description.Should().NotContain("Usage:");
    }

    [Fact]
    public async Task LoadStacksAsync_MoreThanTwoComments_LimitsToTwoLines()
    {
        // Arrange
        var stackContent = @"# Line 1
# Line 2
# Line 3 should be ignored
# Line 4 should also be ignored
version: '3.8'
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "many-comments.yml"), stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.Description.Should().Be("Line 1\nLine 2");
        stack.Description.Should().NotContain("Line 3");
    }

    [Fact]
    public async Task LoadStacksAsync_NoComments_HasNullDescription()
    {
        // Arrange
        var stackContent = @"version: '3.8'
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "no-comments.yml"), stackContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.Description.Should().BeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task LoadStacksAsync_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var source = CreateLocalSource(emptyDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadStacksAsync_InvalidYaml_SkipsAndContinues()
    {
        // Arrange
        var invalidContent = "this is not valid yaml: [";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "invalid.yml"), invalidContent);

        var validContent = @"
version: '3.8'
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "valid.yml"), validContent);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        stacks.First().Name.Should().Be("valid");
    }

    [Fact]
    public async Task LoadStacksAsync_DisabledSource_ReturnsEmpty()
    {
        // Arrange
        var stackContent = @"
version: '3.8'
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.yml"), stackContent);

        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("disabled"),
            "Disabled Source",
            _tempDir,
            "*.yml");
        source.Disable();

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static StackSource CreateLocalSource(string path)
    {
        return StackSource.CreateLocalDirectory(
            new StackSourceId("test-source"),
            "Test Source",
            path,
            "*.yml;*.yaml");
    }

    #endregion
}
