using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.Stacks;
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

        var source = new LocalDirectoryStackSource
        {
            Id = "disabled",
            Name = "Disabled Source",
            Enabled = false,
            Path = _tempDir,
            FilePattern = "*.yml"
        };

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static LocalDirectoryStackSource CreateLocalSource(string path)
    {
        return new LocalDirectoryStackSource
        {
            Id = "test-source",
            Name = "Test Source",
            Enabled = true,
            Path = path,
            FilePattern = "*.yml;*.yaml"
        };
    }

    #endregion
}
