using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;
using ReadyStackGo.Infrastructure.Manifests;
using ReadyStackGo.Infrastructure.Stacks.Sources;

namespace ReadyStackGo.IntegrationTests.Services;

/// <summary>
/// End-to-end tests for Multi-Stack YAML generation.
/// Tests the complete workflow from loading multi-stack manifests to generating deployable docker-compose YAML.
/// </summary>
public class MultiStackYamlGenerationE2ETests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalDirectoryStackSourceProvider _provider;
    private readonly RsgoManifestParser _manifestParser;
    private readonly ILogger<LocalDirectoryStackSourceProvider> _providerLogger;
    private readonly ILogger<RsgoManifestParser> _parserLogger;

    public MultiStackYamlGenerationE2ETests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _providerLogger = Substitute.For<ILogger<LocalDirectoryStackSourceProvider>>();
        _parserLogger = Substitute.For<ILogger<RsgoManifestParser>>();
        _manifestParser = new RsgoManifestParser(_parserLogger);
        _provider = new LocalDirectoryStackSourceProvider(_providerLogger, _manifestParser);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region End-to-End Multi-Stack Loading Tests

    [Fact]
    public async Task E2E_MultiStackWithIncludes_GeneratesValidDockerComposeYaml()
    {
        // This E2E test simulates a complete multi-stack product setup similar to ams.project:
        // 1. Main manifest with includes for sub-stacks
        // 2. Sub-stack files with full service definitions
        // 3. Provider loads and generates docker-compose compatible YAML

        // Arrange - Create product folder structure
        var productFolder = Path.Combine(_tempDir, "enterprise-suite");
        Directory.CreateDirectory(productFolder);

        // Main manifest
        var mainManifest = @"
metadata:
  name: Enterprise Suite
  description: Complete enterprise application suite
  productVersion: 3.0.0
  category: Enterprise
  tags:
    - enterprise
    - production

sharedVariables:
  REGISTRY:
    label: Container Registry
    description: Docker registry for images
    type: String
    default: registry.example.com
    group: General

stacks:
  logging:
    include: Logging/logging.yaml
  database:
    include: Database/database.yaml
";
        await File.WriteAllTextAsync(Path.Combine(productFolder, "stack.yaml"), mainManifest);

        // Create Logging sub-stack
        var loggingFolder = Path.Combine(productFolder, "Logging");
        Directory.CreateDirectory(loggingFolder);

        var loggingManifest = @"
metadata:
  name: Logging Stack
  description: ELK-based logging infrastructure

variables:
  ES_JAVA_OPTS:
    label: Elasticsearch Java Options
    description: JVM heap settings
    type: String
    default: ""-Xms512m -Xmx512m""
    group: Performance

services:
  elasticsearch:
    image: elasticsearch:8.10.0
    ports:
      - ""9200:9200""
    environment:
      discovery.type: single-node
      ES_JAVA_OPTS: ${ES_JAVA_OPTS}
    volumes:
      - esdata:/usr/share/elasticsearch/data
    restart: unless-stopped

  kibana:
    image: kibana:8.10.0
    ports:
      - ""5601:5601""
    environment:
      ELASTICSEARCH_HOSTS: http://elasticsearch:9200
    dependsOn:
      - elasticsearch
    restart: unless-stopped

volumes:
  esdata: {}
";
        await File.WriteAllTextAsync(Path.Combine(loggingFolder, "logging.yaml"), loggingManifest);

        // Create Database sub-stack
        var databaseFolder = Path.Combine(productFolder, "Database");
        Directory.CreateDirectory(databaseFolder);

        var databaseManifest = @"
metadata:
  name: Database Stack
  description: SQL Server and Redis

variables:
  SA_PASSWORD:
    label: SQL SA Password
    description: SQL Server SA password
    type: Password
    required: true
    group: Security

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    ports:
      - ""1433:1433""
    environment:
      ACCEPT_EULA: ""Y""
      SA_PASSWORD: ${SA_PASSWORD}
    volumes:
      - sqldata:/var/opt/mssql
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    ports:
      - ""6379:6379""
    volumes:
      - redisdata:/data
    restart: unless-stopped

volumes:
  sqldata: {}
  redisdata: {}
";
        await File.WriteAllTextAsync(Path.Combine(databaseFolder, "database.yaml"), databaseManifest);

        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("test-source"),
            "Test Source",
            _tempDir,
            "*.yml;*.yaml");

        // Act
        var stacks = (await _provider.LoadStacksAsync(source, CancellationToken.None)).ToList();

        // Assert - Filter for stacks from the main product (exclude fragment files loaded separately)
        var productStacks = stacks.Where(s => s.ProductName == "Enterprise Suite").ToList();
        productStacks.Should().HaveCount(2);

        // Verify Logging stack
        var loggingStack = productStacks.FirstOrDefault(s => s.Name == "Logging Stack");
        loggingStack.Should().NotBeNull();
        loggingStack!.ProductName.Should().Be("Enterprise Suite");
        loggingStack.ProductVersion.Should().Be("3.0.0");
        loggingStack.Services.Should().Contain("elasticsearch");
        loggingStack.Services.Should().Contain("kibana");

        // Verify YAML content is docker-compose compatible
        loggingStack.YamlContent.Should().Contain("services:");
        loggingStack.YamlContent.Should().Contain("elasticsearch:");
        loggingStack.YamlContent.Should().Contain("kibana:");
        loggingStack.YamlContent.Should().Contain("elasticsearch:8.10.0");
        loggingStack.YamlContent.Should().Contain("volumes:");
        loggingStack.YamlContent.Should().Contain("esdata:");

        // Should NOT contain metadata or stacks sections (those are RSGo-specific)
        loggingStack.YamlContent.Should().NotContain("metadata:");
        loggingStack.YamlContent.Should().NotContain("stacks:");

        // Verify Database stack
        var databaseStack = productStacks.FirstOrDefault(s => s.Name == "Database Stack");
        databaseStack.Should().NotBeNull();
        databaseStack!.ProductName.Should().Be("Enterprise Suite");
        databaseStack.Services.Should().Contain("sqlserver");
        databaseStack.Services.Should().Contain("redis");

        // Verify YAML content
        databaseStack.YamlContent.Should().Contain("services:");
        databaseStack.YamlContent.Should().Contain("sqlserver:");
        databaseStack.YamlContent.Should().Contain("mcr.microsoft.com/mssql/server:2022-latest");
        databaseStack.YamlContent.Should().Contain("${SA_PASSWORD}");
    }

    [Fact]
    public async Task E2E_MultiStackYaml_CanBeValidatedByDockerComposeParser()
    {
        // This test verifies that generated YAML can be parsed by DockerComposeParser
        // (which is what the deployment engine uses)

        // Arrange
        var productFolder = Path.Combine(_tempDir, "simple-product");
        Directory.CreateDirectory(productFolder);

        var manifest = @"
metadata:
  name: Simple Product
  productVersion: 1.0.0

stacks:
  web:
    metadata:
      name: Web App
    services:
      nginx:
        image: nginx:alpine
        ports:
          - ""80:80""
        restart: unless-stopped
";
        await File.WriteAllTextAsync(Path.Combine(productFolder, "stack.yaml"), manifest);

        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("test-source"),
            "Test Source",
            _tempDir,
            "*.yml;*.yaml");

        // Act
        var stacks = (await _provider.LoadStacksAsync(source, CancellationToken.None)).ToList();

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();

        // The YAML should be valid docker-compose format
        stack.YamlContent.Should().Contain("services:");
        stack.YamlContent.Should().Contain("nginx:");
        stack.YamlContent.Should().Contain("image: nginx:alpine");

        // Use the actual DockerComposeParser to validate
        var composeParserLogger = Substitute.For<ILogger<ReadyStackGo.Infrastructure.Manifests.DockerComposeParser>>();
        var composeParser = new ReadyStackGo.Infrastructure.Manifests.DockerComposeParser(composeParserLogger);

        var validationResult = await composeParser.ValidateAsync(stack.YamlContent);

        validationResult.IsValid.Should().BeTrue("Generated YAML should be valid docker-compose format");
        validationResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task E2E_MultiStackWithNetworks_GeneratesNetworkDefinitions()
    {
        // Arrange
        var productFolder = Path.Combine(_tempDir, "networked-product");
        Directory.CreateDirectory(productFolder);

        var manifest = @"
metadata:
  name: Networked Product
  productVersion: 1.0.0

stacks:
  app:
    metadata:
      name: Application
    services:
      frontend:
        image: frontend:latest
        networks:
          - frontend-net
          - backend-net
      backend:
        image: backend:latest
        networks:
          - backend-net
    networks:
      frontend-net:
        driver: bridge
      backend-net:
        driver: bridge
";
        await File.WriteAllTextAsync(Path.Combine(productFolder, "stack.yaml"), manifest);

        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("test-source"),
            "Test Source",
            _tempDir,
            "*.yml;*.yaml");

        // Act
        var stacks = (await _provider.LoadStacksAsync(source, CancellationToken.None)).ToList();

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();

        stack.YamlContent.Should().Contain("networks:");
        stack.YamlContent.Should().Contain("frontend-net");
        stack.YamlContent.Should().Contain("backend-net");
    }

    [Fact]
    public async Task E2E_MultiStackWithHealthChecks_GeneratesDockerHealthChecks()
    {
        // Arrange
        var productFolder = Path.Combine(_tempDir, "healthcheck-product");
        Directory.CreateDirectory(productFolder);

        var manifest = @"
metadata:
  name: HealthCheck Product
  productVersion: 1.0.0

stacks:
  app:
    metadata:
      name: Application
    services:
      api:
        image: myapi:latest
        healthCheck:
          test:
            - CMD
            - curl
            - -f
            - http://localhost/health
          interval: 30s
          timeout: 10s
          retries: 3
          startPeriod: 5s
";
        await File.WriteAllTextAsync(Path.Combine(productFolder, "stack.yaml"), manifest);

        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("test-source"),
            "Test Source",
            _tempDir,
            "*.yml;*.yaml");

        // Act
        var stacks = (await _provider.LoadStacksAsync(source, CancellationToken.None)).ToList();

        // Assert
        stacks.Should().HaveCount(1);
        var stack = stacks.First();

        stack.YamlContent.Should().Contain("healthcheck:");
        stack.YamlContent.Should().Contain("interval: 30s");
        stack.YamlContent.Should().Contain("timeout: 10s");
        stack.YamlContent.Should().Contain("retries: 3");
    }

    [Fact]
    public async Task E2E_MultiStack_SharedVariablesAreMergedCorrectly()
    {
        // Arrange
        var productFolder = Path.Combine(_tempDir, "shared-vars-product");
        Directory.CreateDirectory(productFolder);

        // Main manifest with shared variables
        var mainManifest = @"
metadata:
  name: Shared Vars Product
  productVersion: 1.0.0

sharedVariables:
  LOG_LEVEL:
    label: Log Level
    default: INFO
    group: Logging

stacks:
  app:
    include: app/app.yaml
";
        await File.WriteAllTextAsync(Path.Combine(productFolder, "stack.yaml"), mainManifest);

        // Sub-stack with its own variables
        var appFolder = Path.Combine(productFolder, "app");
        Directory.CreateDirectory(appFolder);

        var appManifest = @"
metadata:
  name: App Stack

variables:
  APP_PORT:
    label: App Port
    default: ""8080""
    type: Port

services:
  app:
    image: myapp:latest
    environment:
      LOG_LEVEL: ${LOG_LEVEL}
      PORT: ${APP_PORT}
";
        await File.WriteAllTextAsync(Path.Combine(appFolder, "app.yaml"), appManifest);

        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("test-source"),
            "Test Source",
            _tempDir,
            "*.yml;*.yaml");

        // Act
        var stacks = (await _provider.LoadStacksAsync(source, CancellationToken.None)).ToList();

        // Assert - Filter for stacks from the main product (exclude fragment files loaded separately)
        var productStacks = stacks.Where(s => s.ProductName == "Shared Vars Product").ToList();
        productStacks.Should().HaveCount(1);
        var stack = productStacks.First();

        // Should have both shared and stack-specific variables
        stack.Variables.Should().Contain(v => v.Name == "LOG_LEVEL");
        stack.Variables.Should().Contain(v => v.Name == "APP_PORT");

        // YAML should contain variable references
        stack.YamlContent.Should().Contain("${LOG_LEVEL}");
        stack.YamlContent.Should().Contain("${APP_PORT}");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task E2E_MultiStack_WithMissingIncludeFile_SkipsStack()
    {
        // Arrange
        var productFolder = Path.Combine(_tempDir, "missing-include");
        Directory.CreateDirectory(productFolder);

        var manifest = @"
metadata:
  name: Missing Include Product
  productVersion: 1.0.0

stacks:
  existing:
    metadata:
      name: Existing Stack
    services:
      app:
        image: app:latest
  missing:
    include: nonexistent/missing.yaml
";
        await File.WriteAllTextAsync(Path.Combine(productFolder, "stack.yaml"), manifest);

        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("test-source"),
            "Test Source",
            _tempDir,
            "*.yml;*.yaml");

        // Act
        var stacks = (await _provider.LoadStacksAsync(source, CancellationToken.None)).ToList();

        // Assert - Should still load the existing stack
        stacks.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    #endregion
}
