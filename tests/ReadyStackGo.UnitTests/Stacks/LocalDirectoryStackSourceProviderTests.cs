using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;
using ReadyStackGo.Infrastructure.Stacks.Sources;

namespace ReadyStackGo.UnitTests.Stacks;

/// <summary>
/// Tests for LocalDirectoryStackSourceProvider with RSGo Manifest Format.
/// The provider now only supports stack.yaml/stack.yml (RSGo Manifest Format).
/// </summary>
public class LocalDirectoryStackSourceProviderTests : IDisposable
{
    private readonly Mock<ILogger<LocalDirectoryStackSourceProvider>> _loggerMock;
    private readonly Mock<IRsgoManifestParser> _manifestParserMock;
    private readonly LocalDirectoryStackSourceProvider _provider;
    private readonly string _tempDir;

    public LocalDirectoryStackSourceProviderTests()
    {
        _loggerMock = new Mock<ILogger<LocalDirectoryStackSourceProvider>>();
        _manifestParserMock = new Mock<IRsgoManifestParser>();
        _provider = new LocalDirectoryStackSourceProvider(_loggerMock.Object, _manifestParserMock.Object);
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
metadata:
  name: simple-nginx
  description: Simple Nginx Stack
  productVersion: 1.0.0
services:
  web:
    image: nginx:latest
    ports:
      - '8080:80'
";
        var stackFile = Path.Combine(_tempDir, "simple-nginx.yaml");
        await File.WriteAllTextAsync(stackFile, stackContent);

        SetupManifestParser("simple-nginx", "Simple Nginx Stack", new[] { "web" });
        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("simple-nginx");
        stack.Description.Should().Be("Simple Nginx Stack");
        stack.Services.Should().Contain("web");
    }

    [Fact]
    public async Task LoadStacksAsync_WithVariables_ExtractsVariables()
    {
        // Arrange
        var stackContent = @"
metadata:
  name: nginx-vars
  productVersion: 1.0.0
variables:
  NGINX_VERSION:
    label: Nginx Version
    default: latest
    type: String
  PORT:
    label: Port
    default: '8080'
    type: Port
services:
  web:
    image: nginx:${NGINX_VERSION}
";
        var stackFile = Path.Combine(_tempDir, "nginx-vars.yaml");
        await File.WriteAllTextAsync(stackFile, stackContent);

        var variables = new List<StackVariable>
        {
            new("NGINX_VERSION", "latest", "Nginx Version", VariableType.String),
            new("PORT", "8080", "Port", VariableType.Port)
        };
        SetupManifestParser("nginx-vars", null, new[] { "web" }, variables);

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
metadata:
  name: mysql
  productVersion: 1.0.0
variables:
  DB_PASSWORD:
    label: Database Password
    required: true
    type: Password
  DB_NAME:
    label: Database Name
    default: myapp
    type: String
services:
  db:
    image: mysql:8.0
";
        var stackFile = Path.Combine(_tempDir, "mysql.yaml");
        await File.WriteAllTextAsync(stackFile, stackContent);

        var variables = new List<StackVariable>
        {
            new("DB_PASSWORD", null, "Database Password", VariableType.Password, isRequired: true),
            new("DB_NAME", "myapp", "Database Name", VariableType.String, isRequired: false)
        };
        SetupManifestParser("mysql", null, new[] { "db" }, variables);

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
metadata:
  name: WordPress
  description: WordPress with MySQL
  productVersion: 6.0.0
services:
  wordpress:
    image: wordpress:latest
  db:
    image: mysql:8.0
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "stack.yaml"), composeContent);

        SetupManifestParser("WordPress", "WordPress with MySQL", new[] { "wordpress", "db" });
        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("WordPress");
        stack.Description.Should().Be("WordPress with MySQL");
        stack.Services.Should().HaveCount(2);
        stack.Services.Should().Contain("wordpress");
        stack.Services.Should().Contain("db");
    }

    [Fact]
    public async Task LoadStacksAsync_FolderWithDockerCompose_LoadsFromFolder()
    {
        // Arrange - legacy support for docker-compose.yml
        var stackFolder = Path.Combine(_tempDir, "legacy-app");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
metadata:
  name: Legacy App
  productVersion: 1.0.0
services:
  app:
    image: myapp:latest
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "docker-compose.yml"), composeContent);

        SetupManifestParser("Legacy App", null, new[] { "app" });
        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("Legacy App");
    }

    #endregion

    #region Recursive Search Tests

    [Fact]
    public async Task LoadStacksAsync_NestedFolderStack_LoadsWithRelativePath()
    {
        // Arrange
        var examplesDir = Path.Combine(_tempDir, "examples");
        var wordpressDir = Path.Combine(examplesDir, "wordpress");
        Directory.CreateDirectory(wordpressDir);

        var composeContent = @"
metadata:
  name: WordPress
  productVersion: 1.0.0
services:
  wordpress:
    image: wordpress:latest
";
        await File.WriteAllTextAsync(Path.Combine(wordpressDir, "stack.yaml"), composeContent);

        SetupManifestParser("WordPress", null, new[] { "wordpress" });
        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("WordPress");
        stack.RelativePath.Should().Be("examples");
    }

    [Fact]
    public async Task LoadStacksAsync_DeeplyNestedFolderStack_LoadsWithFullRelativePath()
    {
        // Arrange
        var projectDir = Path.Combine(_tempDir, "ams.project");
        var identityDir = Path.Combine(projectDir, "identityaccess");
        Directory.CreateDirectory(identityDir);

        var composeContent = @"
metadata:
  name: IdentityAccess
  productVersion: 1.0.0
services:
  identity-api:
    image: identity:latest
";
        await File.WriteAllTextAsync(Path.Combine(identityDir, "stack.yaml"), composeContent);

        SetupManifestParser("IdentityAccess", null, new[] { "identity-api" });
        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("IdentityAccess");
        stack.RelativePath.Should().Be("ams.project");
    }

    [Fact]
    public async Task LoadStacksAsync_FileBasedStackInSubdirectory_LoadsWithRelativePath()
    {
        // Arrange
        var examplesDir = Path.Combine(_tempDir, "examples");
        Directory.CreateDirectory(examplesDir);

        var stackContent = @"
metadata:
  name: simple-nginx
  productVersion: 1.0.0
services:
  nginx:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(examplesDir, "simple-nginx.yaml"), stackContent);

        SetupManifestParser("simple-nginx", null, new[] { "nginx" });
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
        // Arrange
        var stackFolder = Path.Combine(_tempDir, "myapp");
        Directory.CreateDirectory(stackFolder);

        var composeContent = @"
metadata:
  name: MyApp
  productVersion: 1.0.0
services:
  app:
    image: app:latest
";
        await File.WriteAllTextAsync(Path.Combine(stackFolder, "stack.yaml"), composeContent);

        SetupManifestParser("MyApp", null, new[] { "app" });
        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("MyApp");
        stack.RelativePath.Should().BeNull();
    }

    [Fact]
    public async Task LoadStacksAsync_FileAtRootLevel_HasNullRelativePath()
    {
        // Arrange
        var stackContent = @"
metadata:
  name: Simple
  productVersion: 1.0.0
services:
  simple:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "simple.yaml"), stackContent);

        SetupManifestParser("Simple", null, new[] { "simple" });
        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();
        stack.Name.Should().Be("Simple");
        stack.RelativePath.Should().BeNull();
    }

    [Fact]
    public async Task LoadStacksAsync_MultipleNestedLevels_LoadsAllRecursively()
    {
        // Arrange
        var level1 = Path.Combine(_tempDir, "level1");
        var stack1 = Path.Combine(level1, "stack1");
        Directory.CreateDirectory(stack1);

        var level2 = Path.Combine(level1, "level2");
        var stack2 = Path.Combine(level2, "stack2");
        Directory.CreateDirectory(stack2);

        var level3 = Path.Combine(level2, "level3");
        var stack3 = Path.Combine(level3, "stack3");
        Directory.CreateDirectory(stack3);

        // Setup different parser responses for each stack based on file path
        _manifestParserMock.Setup(p => p.ParseFromFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string filePath, CancellationToken _) =>
            {
                // Parse stack name from file path
                if (filePath.Contains("stack1"))
                    return CreateManifest("Stack1", null, new[] { "app" });
                if (filePath.Contains("stack2"))
                    return CreateManifest("Stack2", null, new[] { "app" });
                if (filePath.Contains("stack3"))
                    return CreateManifest("Stack3", null, new[] { "app" });
                return CreateManifest("Unknown", null, new[] { "app" });
            });
        _manifestParserMock.Setup(p => p.ExtractVariablesAsync(It.IsAny<RsgoManifest>()))
            .ReturnsAsync(new List<StackVariable>());

        var composeTemplate1 = @"
metadata:
  name: Stack1
  productVersion: 1.0.0
services:
  app:
    image: app:latest
";
        var composeTemplate2 = @"
metadata:
  name: Stack2
  productVersion: 1.0.0
services:
  app:
    image: app:latest
";
        var composeTemplate3 = @"
metadata:
  name: Stack3
  productVersion: 1.0.0
services:
  app:
    image: app:latest
";
        await File.WriteAllTextAsync(Path.Combine(stack1, "stack.yaml"), composeTemplate1);
        await File.WriteAllTextAsync(Path.Combine(stack2, "stack.yaml"), composeTemplate2);
        await File.WriteAllTextAsync(Path.Combine(stack3, "stack.yaml"), composeTemplate3);

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(3);
        stacks.Should().Contain(s => s.Name == "Stack1");
        stacks.Should().Contain(s => s.Name == "Stack2");
        stacks.Should().Contain(s => s.Name == "Stack3");

        var s1 = stacks.First(s => s.Name == "Stack1");
        s1.RelativePath.Should().Be("level1");

        var s2 = stacks.First(s => s.Name == "Stack2");
        s2.RelativePath.Should().Be(Path.Combine("level1", "level2"));

        var s3 = stacks.First(s => s.Name == "Stack3");
        s3.RelativePath.Should().Be(Path.Combine("level1", "level2", "level3"));
    }

    #endregion

    #region Description from Metadata Tests

    [Fact]
    public async Task LoadStacksAsync_WithMetadataDescription_ExtractsDescription()
    {
        // Arrange
        var stackContent = @"
metadata:
  name: My Stack
  description: This is a detailed description of my stack
  productVersion: 1.0.0
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "mystack.yaml"), stackContent);

        SetupManifestParser("My Stack", "This is a detailed description of my stack", new[] { "web" });
        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        var stack = stacks.First();
        stack.Description.Should().Be("This is a detailed description of my stack");
    }

    [Fact]
    public async Task LoadStacksAsync_NoDescription_HasNullDescription()
    {
        // Arrange
        var stackContent = @"
metadata:
  name: No Description Stack
  productVersion: 1.0.0
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "no-desc.yaml"), stackContent);

        SetupManifestParser("No Description Stack", null, new[] { "web" });
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
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "broken.yaml"), invalidContent);

        var validContent = @"
metadata:
  name: Valid
  productVersion: 1.0.0
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "working.yaml"), validContent);

        // Setup parser to throw for invalid yaml based on file path
        _manifestParserMock.Setup(p => p.ParseFromFileAsync(It.Is<string>(s => s.Contains("broken")), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Invalid YAML"));
        _manifestParserMock.Setup(p => p.ParseFromFileAsync(It.Is<string>(s => s.Contains("working")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateManifest("Valid", null, new[] { "web" }));
        _manifestParserMock.Setup(p => p.ExtractVariablesAsync(It.IsAny<RsgoManifest>()))
            .ReturnsAsync(new List<StackVariable>());

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(1);
        stacks.First().Name.Should().Be("Valid");
    }

    [Fact]
    public async Task LoadStacksAsync_DisabledSource_ReturnsEmpty()
    {
        // Arrange
        var stackContent = @"
metadata:
  name: Test
  productVersion: 1.0.0
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.yaml"), stackContent);

        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("disabled"),
            "Disabled Source",
            _tempDir,
            "*.yml;*.yaml");
        source.Disable();

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadStacksAsync_MixedFolderAndFileStacks_LoadsBoth()
    {
        // Arrange
        // File-based stack
        var fileContent = @"
metadata:
  name: Redis
  productVersion: 1.0.0
services:
  redis:
    image: redis:alpine
";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "redis.yaml"), fileContent);

        // Folder-based stack
        var folderPath = Path.Combine(_tempDir, "postgres");
        Directory.CreateDirectory(folderPath);
        var folderContent = @"
metadata:
  name: Postgres
  productVersion: 15.0.0
services:
  db:
    image: postgres:15
";
        await File.WriteAllTextAsync(Path.Combine(folderPath, "stack.yaml"), folderContent);

        _manifestParserMock.Setup(p => p.ParseFromFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string filePath, CancellationToken _) =>
            {
                if (filePath.Contains("redis"))
                    return CreateManifest("Redis", null, new[] { "redis" });
                if (filePath.Contains("postgres"))
                    return CreateManifest("Postgres", null, new[] { "db" });
                return CreateManifest("Unknown", null, new[] { "app" });
            });
        _manifestParserMock.Setup(p => p.ExtractVariablesAsync(It.IsAny<RsgoManifest>()))
            .ReturnsAsync(new List<StackVariable>());

        var source = CreateLocalSource(_tempDir);

        // Act
        var stacks = await _provider.LoadStacksAsync(source, CancellationToken.None);

        // Assert
        stacks.Should().HaveCount(2);
        stacks.Should().Contain(s => s.Name == "Redis");
        stacks.Should().Contain(s => s.Name == "Postgres");
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

    private void SetupManifestParser(string name, string? description, string[] services, List<StackVariable>? variables = null)
    {
        var manifest = CreateManifest(name, description, services);
        _manifestParserMock.Setup(p => p.ParseFromFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        _manifestParserMock.Setup(p => p.ExtractVariablesAsync(It.IsAny<RsgoManifest>()))
            .ReturnsAsync(variables ?? new List<StackVariable>());
    }

    private static RsgoManifest CreateManifest(string name, string? description, string[] services)
    {
        return new RsgoManifest
        {
            Metadata = new RsgoProductMetadata
            {
                Name = name,
                Description = description,
                ProductVersion = "1.0.0"
            },
            Services = services.ToDictionary(s => s, s => new RsgoService { Image = $"{s}:latest" })
        };
    }

    #endregion
}
