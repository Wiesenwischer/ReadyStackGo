using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Infrastructure.Parsing;

namespace ReadyStackGo.UnitTests.Manifests;

public class RsgoManifestParserTests
{
    private readonly RsgoManifestParser _parser;

    public RsgoManifestParserTests()
    {
        var logger = new Mock<ILogger<RsgoManifestParser>>();
        _parser = new RsgoManifestParser(logger.Object);
    }

    [Fact]
    public async Task ParseAsync_ValidManifest_ReturnsDefinition()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: WordPress
  description: WordPress with MySQL backend
  category: CMS
services:
  wordpress:
    image: wordpress:latest
    ports:
      - '8080:80'
  db:
    image: mysql:8.0
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be("1.0");
        result.Metadata.Name.Should().Be("WordPress");
        result.Metadata.Description.Should().Be("WordPress with MySQL backend");
        result.Services.Should().HaveCount(2);
        result.Services.Should().ContainKey("wordpress");
        result.Services.Should().ContainKey("db");
    }

    [Fact]
    public async Task ParseAsync_WithVariables_ParsesTypes()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  PORT:
    label: Web Port
    type: Port
    default: '8080'
    required: true
  PASSWORD:
    label: Database Password
    type: Password
    required: true
  ENABLE_DEBUG:
    label: Enable Debug Mode
    type: Boolean
    default: 'false'
  DB_TYPE:
    label: Database Type
    type: Select
    options:
      - value: mysql
        label: MySQL
      - value: postgres
        label: PostgreSQL
    default: mysql
services:
  app:
    image: myapp:latest
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Variables.Should().HaveCount(4);

        result.Variables["PORT"].Type.Should().Be(VariableType.Port);
        result.Variables["PORT"].Default.Should().Be("8080");
        result.Variables["PORT"].Required.Should().BeTrue();

        result.Variables["PASSWORD"].Type.Should().Be(VariableType.Password);
        result.Variables["PASSWORD"].Required.Should().BeTrue();

        result.Variables["ENABLE_DEBUG"].Type.Should().Be(VariableType.Boolean);
        result.Variables["ENABLE_DEBUG"].Default.Should().Be("false");

        result.Variables["DB_TYPE"].Type.Should().Be(VariableType.Select);
        result.Variables["DB_TYPE"].Options.Should().HaveCount(2);
        result.Variables["DB_TYPE"].Default.Should().Be("mysql");
    }

    [Fact]
    public async Task ParseAsync_WithPatternValidation_ParsesPattern()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  EMAIL:
    label: Admin Email
    type: String
    pattern: '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$'
    patternError: Please enter a valid email address
    required: true
services:
  app:
    image: myapp:latest
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Variables!["EMAIL"].Pattern.Should().NotBeNullOrEmpty();
        result.Variables["EMAIL"].PatternError.Should().Be("Please enter a valid email address");
    }

    [Fact]
    public async Task ExtractVariablesAsync_ReturnsStackVariables()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  PORT:
    label: Web Port
    description: The port to expose the application on
    type: Port
    default: '8080'
    min: 1
    max: 65535
    group: Network
    order: 1
services:
  app:
    image: myapp:latest
";
        var manifest = await _parser.ParseAsync(yaml);

        // Act
        var variables = await _parser.ExtractVariablesAsync(manifest);

        // Assert
        variables.Should().HaveCount(1);
        var portVar = variables.First();
        portVar.Name.Should().Be("PORT");
        portVar.Label.Should().Be("Web Port");
        portVar.Description.Should().Be("The port to expose the application on");
        portVar.Type.Should().Be(VariableType.Port);
        portVar.DefaultValue.Should().Be("8080");
        portVar.Min.Should().Be(1);
        portVar.Max.Should().Be(65535);
        portVar.Group.Should().Be("Network");
        portVar.Order.Should().Be(1);
    }

    [Fact]
    public async Task ConvertToDeploymentPlanAsync_CreatesCorrectPlan()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  productVersion: '1.0.0'
services:
  web:
    image: nginx:${VERSION}
    ports:
      - '${PORT}:80'
    environment:
      DEBUG: '${DEBUG}'
  db:
    image: postgres:15
";
        var manifest = await _parser.ParseAsync(yaml);
        var variables = new Dictionary<string, string>
        {
            ["VERSION"] = "1.21",
            ["PORT"] = "8080",
            ["DEBUG"] = "true"
        };

        // Act
        var plan = await _parser.ConvertToDeploymentPlanAsync(manifest, variables, "my-stack");

        // Assert
        plan.Should().NotBeNull();
        plan.StackVersion.Should().Be("1.0.0");
        plan.Steps.Should().HaveCount(2);

        var webStep = plan.Steps.First(s => s.ContextName == "web");
        webStep.Image.Should().Be("nginx:1.21");
        webStep.Ports.Should().Contain("8080:80");
        webStep.EnvVars["DEBUG"].Should().Be("true");
    }

    [Fact]
    public async Task ValidateAsync_ValidManifest_ReturnsValid()
    {
        // Arrange
        var yaml = @"
version: '1.0'
services:
  web:
    image: nginx:latest
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_NoServices_ReturnsInvalid()
    {
        // Arrange
        var yaml = @"
version: '1.0'
services: {}
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("services") || e.Contains("stacks"));
    }

    [Fact]
    public async Task ValidateAsync_SelectWithoutOptions_AddsWarning()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  TYPE:
    type: Select
services:
  app:
    image: myapp:latest
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Select") && w.Contains("options"));
    }

    [Fact]
    public async Task ValidateAsync_InvalidRegex_ReturnsInvalid()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  EMAIL:
    type: String
    pattern: '[invalid('
services:
  app:
    image: myapp:latest
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("regex") || e.Contains("pattern"));
    }

    [Fact]
    public async Task ValidateVariablesAsync_ValidValues_ReturnsValid()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  PORT:
    type: Port
    required: true
  EMAIL:
    type: String
    pattern: '^[^@]+@[^@]+$'
services:
  app:
    image: myapp:latest
";
        var manifest = await _parser.ParseAsync(yaml);
        var values = new Dictionary<string, string>
        {
            ["PORT"] = "8080",
            ["EMAIL"] = "test@example.com"
        };

        // Act
        var result = await _parser.ValidateVariablesAsync(manifest, values);

        // Assert
        result.IsValid.Should().BeTrue();
        result.VariableErrors.Should().BeEmpty();
        result.MissingRequired.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateVariablesAsync_MissingRequired_ReturnsInvalid()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  PORT:
    type: Port
    required: true
services:
  app:
    image: myapp:latest
";
        var manifest = await _parser.ParseAsync(yaml);
        var values = new Dictionary<string, string>();

        // Act
        var result = await _parser.ValidateVariablesAsync(manifest, values);

        // Assert
        result.IsValid.Should().BeFalse();
        result.MissingRequired.Should().Contain("PORT");
    }

    [Fact]
    public async Task ValidateVariablesAsync_InvalidPort_ReturnsError()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  PORT:
    type: Port
    required: true
services:
  app:
    image: myapp:latest
";
        var manifest = await _parser.ParseAsync(yaml);
        var values = new Dictionary<string, string>
        {
            ["PORT"] = "99999"
        };

        // Act
        var result = await _parser.ValidateVariablesAsync(manifest, values);

        // Assert
        result.IsValid.Should().BeFalse();
        result.VariableErrors.Should().ContainKey("PORT");
    }

    [Fact]
    public void DetectFormat_DockerCompose_ReturnsDockerCompose()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  web:
    image: nginx:latest
";

        // Act
        var format = _parser.DetectFormat(yaml);

        // Assert
        format.Should().Be(ManifestFormat.DockerCompose);
    }

    [Fact]
    public void DetectFormat_RsgoManifest_ReturnsRsgoManifest()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: My Stack
variables:
  PORT:
    type: Port
services:
  web:
    image: nginx:latest
";

        // Act
        var format = _parser.DetectFormat(yaml);

        // Assert
        format.Should().Be(ManifestFormat.RsgoManifest);
    }

    [Fact]
    public void DetectFormat_WithVariablesSection_ReturnsRsgoManifest()
    {
        // Arrange
        var yaml = @"
version: '1.0'
variables:
  PORT:
    type: Port
services:
  web:
    image: nginx:latest
";

        // Act
        var format = _parser.DetectFormat(yaml);

        // Assert
        format.Should().Be(ManifestFormat.RsgoManifest);
    }

    #region Multi-Stack Support Tests

    [Fact]
    public async Task ParseAsync_ProductManifest_IsProductReturnsTrue()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: My Product
  productVersion: '3.1.0'
  category: Enterprise
services:
  web:
    image: nginx:latest
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.IsProduct.Should().BeTrue();
        result.Metadata.ProductVersion.Should().Be("3.1.0");
    }

    [Fact]
    public async Task ParseAsync_FragmentManifest_IsProductReturnsFalse()
    {
        // Arrange - No productVersion = Fragment
        var yaml = @"
version: '1.0'
metadata:
  name: Identity Stack
  description: Identity Provider
services:
  identity-api:
    image: myapp/identity:latest
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.IsProduct.Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_MultiStackManifest_ParsesStacks()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: ams.project
  productVersion: '3.1.0'
stacks:
  identity:
    metadata:
      name: Identity Access
      description: Identity Provider
    variables:
      PORT:
        type: Port
        default: '7614'
    services:
      identity-api:
        image: myapp/identity:latest
        ports:
          - '${PORT}:8080'
  monitoring:
    metadata:
      name: Monitoring Stack
    services:
      prometheus:
        image: prom/prometheus:latest
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.IsMultiStack.Should().BeTrue();
        result.IsSingleStack.Should().BeFalse();
        result.Stacks.Should().HaveCount(2);
        result.Stacks.Should().ContainKey("identity");
        result.Stacks.Should().ContainKey("monitoring");
        result.Stacks!["identity"].Metadata!.Name.Should().Be("Identity Access");
        result.Stacks["identity"].Services.Should().ContainKey("identity-api");
    }

    [Fact]
    public async Task ParseAsync_MultiStackWithIncludes_ParsesIncludePaths()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: ams.project
  productVersion: '3.1.0'
stacks:
  identity:
    include: identity-access.yaml
  infrastructure:
    include: infrastructure.yaml
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.IsMultiStack.Should().BeTrue();
        result.Stacks!["identity"].IsInclude.Should().BeTrue();
        result.Stacks["identity"].Include.Should().Be("identity-access.yaml");
        result.Stacks["infrastructure"].IsInclude.Should().BeTrue();
        result.Stacks["infrastructure"].Include.Should().Be("infrastructure.yaml");
    }

    [Fact]
    public async Task ParseAsync_SharedVariables_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: Multi-Stack Product
  productVersion: '1.0.0'
sharedVariables:
  REGISTRY:
    label: Docker Registry
    type: String
    default: myregistry
  ENVIRONMENT:
    label: Environment
    type: Select
    options:
      - value: dev
        label: Development
      - value: prod
        label: Production
    default: dev
stacks:
  web:
    services:
      nginx:
        image: ${REGISTRY}/nginx:latest
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.SharedVariables.Should().HaveCount(2);
        result.SharedVariables.Should().ContainKey("REGISTRY");
        result.SharedVariables.Should().ContainKey("ENVIRONMENT");
        result.SharedVariables!["REGISTRY"].Default.Should().Be("myregistry");
        result.SharedVariables["ENVIRONMENT"].Type.Should().Be(VariableType.Select);
    }

    [Fact]
    public async Task ExtractVariablesAsync_MultiStack_MergesSharedAndStackVariables()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: Test Product
  productVersion: '1.0.0'
sharedVariables:
  REGISTRY:
    label: Registry
    type: String
    default: shared-registry
  SHARED_VAR:
    label: Shared
    type: String
    default: shared-value
stacks:
  web:
    variables:
      PORT:
        label: Port
        type: Port
        default: '8080'
      REGISTRY:
        label: Registry Override
        type: String
        default: stack-registry
    services:
      nginx:
        image: nginx:latest
";
        var manifest = await _parser.ParseAsync(yaml);

        // Act
        var variables = await _parser.ExtractVariablesAsync(manifest);

        // Assert
        variables.Should().HaveCount(3); // REGISTRY, SHARED_VAR, PORT
        variables.Should().Contain(v => v.Name == "REGISTRY" && v.DefaultValue == "stack-registry"); // Stack overrides shared
        variables.Should().Contain(v => v.Name == "SHARED_VAR" && v.DefaultValue == "shared-value");
        variables.Should().Contain(v => v.Name == "PORT" && v.DefaultValue == "8080");
    }

    [Fact]
    public async Task ExtractStackVariablesAsync_ReturnsStackSpecificVariables()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: Test Product
  productVersion: '1.0.0'
sharedVariables:
  REGISTRY:
    type: String
    default: shared-registry
stacks:
  identity:
    variables:
      IDENTITY_PORT:
        type: Port
        default: '7614'
    services:
      api:
        image: identity:latest
  monitoring:
    variables:
      MONITORING_PORT:
        type: Port
        default: '9090'
    services:
      prometheus:
        image: prometheus:latest
";
        var manifest = await _parser.ParseAsync(yaml);

        // Act
        var identityVars = await _parser.ExtractStackVariablesAsync(manifest, "identity");
        var monitoringVars = await _parser.ExtractStackVariablesAsync(manifest, "monitoring");

        // Assert
        identityVars.Should().HaveCount(2); // REGISTRY + IDENTITY_PORT
        identityVars.Should().Contain(v => v.Name == "REGISTRY");
        identityVars.Should().Contain(v => v.Name == "IDENTITY_PORT");
        identityVars.Should().NotContain(v => v.Name == "MONITORING_PORT");

        monitoringVars.Should().HaveCount(2); // REGISTRY + MONITORING_PORT
        monitoringVars.Should().Contain(v => v.Name == "REGISTRY");
        monitoringVars.Should().Contain(v => v.Name == "MONITORING_PORT");
        monitoringVars.Should().NotContain(v => v.Name == "IDENTITY_PORT");
    }

    [Fact]
    public async Task ConvertStackToDeploymentPlanAsync_MultiStack_CreatesCorrectPlan()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: Multi-Stack
  productVersion: '2.0.0'
stacks:
  web:
    services:
      nginx:
        image: nginx:${VERSION}
        ports:
          - '${PORT}:80'
";
        var manifest = await _parser.ParseAsync(yaml);
        var variables = new Dictionary<string, string>
        {
            ["VERSION"] = "1.21",
            ["PORT"] = "8080"
        };

        // Act
        var plan = await _parser.ConvertStackToDeploymentPlanAsync(manifest, "web", variables, "my-stack");

        // Assert
        plan.Should().NotBeNull();
        plan.StackVersion.Should().Be("2.0.0");
        plan.Steps.Should().HaveCount(1);
        plan.Steps[0].Image.Should().Be("nginx:1.21");
        plan.Steps[0].Ports.Should().Contain("8080:80");
    }

    [Fact]
    public async Task ConvertToDeploymentPlanAsync_MultiStack_ThrowsException()
    {
        // Arrange - Multi-stack manifest without specifying which stack
        var yaml = @"
version: '1.0'
metadata:
  name: Multi-Stack
  productVersion: '1.0.0'
stacks:
  web:
    services:
      nginx:
        image: nginx:latest
  db:
    services:
      postgres:
        image: postgres:15
";
        var manifest = await _parser.ParseAsync(yaml);

        // Act & Assert
        var act = () => _parser.ConvertToDeploymentPlanAsync(manifest, new Dictionary<string, string>(), "stack");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*multi-stack*");
    }

    [Fact]
    public async Task ValidateAsync_MultiStackWithInlineStacks_ValidatesEach()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: Multi-Stack
  productVersion: '1.0.0'
stacks:
  valid-stack:
    services:
      nginx:
        image: nginx:latest
  invalid-stack:
    services:
      broken:
        image: ''
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("invalid-stack") && e.Contains("Image"));
    }

    [Fact]
    public async Task ValidateAsync_MultiStackWithEmptyInclude_ReturnsError()
    {
        // Arrange - Empty include is treated as inline stack without services
        var yaml = @"
version: '1.0'
metadata:
  name: Multi-Stack
  productVersion: '1.0.0'
stacks:
  web:
    include: ''
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        // Empty include is treated as inline definition without services
        result.Errors.Should().Contain(e => e.Contains("services") || e.Contains("Include"));
    }

    [Fact]
    public async Task ValidateAsync_NoProductVersion_AddsWarning()
    {
        // Arrange - Fragment without productVersion
        var yaml = @"
version: '1.0'
metadata:
  name: Fragment Stack
services:
  web:
    image: nginx:latest
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("productVersion") || w.Contains("include"));
    }

    [Fact]
    public async Task ValidateAsync_MixedInlineAndInclude_Validates()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: Mixed Stack
  productVersion: '1.0.0'
stacks:
  inline-stack:
    services:
      nginx:
        image: nginx:latest
  included-stack:
    include: other-stack.yaml
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Include Resolution Integration Tests

    [Fact]
    public async Task ParseFromFileAsync_MultiStackWithIncludes_ResolvesServicesFromIncludeFiles()
    {
        // Arrange - Create temporary test files
        var tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create the main manifest file with includes
            var mainManifest = @"
metadata:
  name: Business Services
  productId: business-services
  description: Business Services - all bounded context services
  productVersion: '3.1.0-pre'
  category: Business

sharedVariables:
  REDIS_CONNECTION:
    label: Redis Connection
    type: String
    default: cachedata:6379

stacks:
  projectmanagement:
    include: projectmanagement.yaml
  memo:
    include: memo.yaml
";
            var mainFilePath = Path.Combine(tempDir, "business-services.yaml");
            await File.WriteAllTextAsync(mainFilePath, mainManifest);

            // Create first include file (projectmanagement.yaml)
            var projectManagementFragment = @"
metadata:
  name: ProjectManagement
  description: Project Management bounded context

services:
  project-api:
    image: amssolution/project-api:latest
    containerName: project-api
    ports:
      - '7700:8080'
    environment:
      REDIS_CONNECTION: ${REDIS_CONNECTION}

  project-web:
    image: amssolution/project-web:latest
    containerName: project-web
    ports:
      - '7701:3000'
";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "projectmanagement.yaml"), projectManagementFragment);

            // Create second include file (memo.yaml)
            var memoFragment = @"
metadata:
  name: Memo
  description: Memo bounded context

services:
  memo-api:
    image: amssolution/memo-api:latest
    containerName: memo-api
    ports:
      - '7702:8080'
    environment:
      REDIS_CONNECTION: ${REDIS_CONNECTION}
";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "memo.yaml"), memoFragment);

            // Act
            var result = await _parser.ParseFromFileAsync(mainFilePath);

            // Assert
            result.Should().NotBeNull();
            result.IsMultiStack.Should().BeTrue();
            result.IsProduct.Should().BeTrue();
            result.Stacks.Should().HaveCount(2);

            // Verify that includes were resolved and services were loaded
            var projectManagementStack = result.Stacks!["projectmanagement"];
            projectManagementStack.Should().NotBeNull();
            projectManagementStack.Services.Should().NotBeNull("Include should be resolved and services loaded");
            projectManagementStack.Services.Should().HaveCount(2, "projectmanagement.yaml defines 2 services");
            projectManagementStack.Services.Should().ContainKey("project-api");
            projectManagementStack.Services.Should().ContainKey("project-web");
            projectManagementStack.Services!["project-api"].Image.Should().Be("amssolution/project-api:latest");

            var memoStack = result.Stacks["memo"];
            memoStack.Should().NotBeNull();
            memoStack.Services.Should().NotBeNull("Include should be resolved and services loaded");
            memoStack.Services.Should().HaveCount(1, "memo.yaml defines 1 service");
            memoStack.Services.Should().ContainKey("memo-api");
            memoStack.Services!["memo-api"].Image.Should().Be("amssolution/memo-api:latest");

            // Verify metadata from include files
            projectManagementStack.Metadata.Should().NotBeNull();
            projectManagementStack.Metadata!.Name.Should().Be("ProjectManagement");
            projectManagementStack.Metadata.Description.Should().Be("Project Management bounded context");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ParseFromFileAsync_MultiStackWithNestedIncludes_ResolvesServicesFromSubdirectory()
    {
        // Arrange - Create temporary test files with subdirectory
        var tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var contextsDir = Path.Combine(tempDir, "Contexts");
        Directory.CreateDirectory(contextsDir);

        try
        {
            // Create the main manifest file with includes in subdirectory
            var mainManifest = @"
metadata:
  name: Business Services
  productVersion: '3.1.0-pre'

sharedVariables:
  EVENTSTORE_DB:
    type: EventStoreConnectionString
    default: 'esdb://admin:changeit@eventstore-db:2113?tls=false'

stacks:
  analytics:
    include: Contexts/analytics.yaml
";
            var mainFilePath = Path.Combine(tempDir, "business-services.yaml");
            await File.WriteAllTextAsync(mainFilePath, mainManifest);

            // Create include file in subdirectory
            var analyticsFragment = @"
metadata:
  name: Analytics
  description: Analytics bounded context services

services:
  analytics-web:
    image: amssolution/analytics-web:linux-v3.1.0-pre
    containerName: analytics-web
    environment:
      NODE_ENV: ${NODE_ENV}
    restart: unless-stopped
";
            await File.WriteAllTextAsync(Path.Combine(contextsDir, "analytics.yaml"), analyticsFragment);

            // Act
            var result = await _parser.ParseFromFileAsync(mainFilePath);

            // Assert
            result.Should().NotBeNull();
            result.IsMultiStack.Should().BeTrue();
            result.Stacks.Should().HaveCount(1);

            var analyticsStack = result.Stacks!["analytics"];
            analyticsStack.Should().NotBeNull();
            analyticsStack.Services.Should().NotBeNull("Include from subdirectory should be resolved");
            analyticsStack.Services.Should().HaveCount(1);
            analyticsStack.Services.Should().ContainKey("analytics-web");
            analyticsStack.Services!["analytics-web"].Image.Should().Be("amssolution/analytics-web:linux-v3.1.0-pre");
            analyticsStack.Services["analytics-web"].Restart.Should().Be("unless-stopped");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    #endregion

    #region Multi-Stack Include Error Cases

    [Fact]
    public async Task ParseFromFileAsync_IncludeReferencesMultiStackProduct_FlattensAllServices()
    {
        // This test verifies the fix for the bug where ams.project includes BusinessServices/business-services.yaml
        // which is itself a multi-stack product. The parser now flattens all services from all sub-stacks
        // into a single stack.

        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var businessDir = Path.Combine(tempDir, "BusinessServices");
        Directory.CreateDirectory(businessDir);

        try
        {
            // Create main product manifest (like ams-project.yaml)
            var mainManifest = @"
metadata:
  name: ams.project
  productVersion: '3.1.0-pre'

stacks:
  business-services:
    include: BusinessServices/business-services.yaml
";
            var mainFilePath = Path.Combine(tempDir, "ams-project.yaml");
            await File.WriteAllTextAsync(mainFilePath, mainManifest);

            // Create multi-stack product as include
            var businessServicesManifest = @"
metadata:
  name: Business Services
  productId: business-services
  productVersion: '3.1.0-pre'

stacks:
  projectmanagement:
    services:
      project-api:
        image: test/project-api:latest
      project-web:
        image: test/project-web:latest
  memo:
    services:
      memo-api:
        image: test/memo-api:latest
";
            await File.WriteAllTextAsync(Path.Combine(businessDir, "business-services.yaml"), businessServicesManifest);

            // Act
            var result = await _parser.ParseFromFileAsync(mainFilePath);

            // Assert - FIX: Services should be flattened from all sub-stacks
            result.Stacks.Should().HaveCount(1);
            var businessStack = result.Stacks!["business-services"];
            businessStack.Should().NotBeNull();

            // All services from all sub-stacks should be collected
            businessStack.Services.Should().NotBeNull("Services should be flattened from multi-stack include");
            businessStack.Services.Should().HaveCount(3, "Should have 3 services: project-api, project-web, memo-api");
            businessStack.Services.Should().ContainKey("project-api");
            businessStack.Services.Should().ContainKey("project-web");
            businessStack.Services.Should().ContainKey("memo-api");

            // Verify metadata is preserved
            businessStack.Metadata.Should().NotBeNull();
            businessStack.Metadata!.Name.Should().Be("Business Services");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    #endregion

    #region Service Include Tests

    [Fact]
    public async Task ParseFromFileAsync_WithServiceIncludes_MergesAllServicesIntoSingleStack()
    {
        // This test verifies the new services.include mechanism for business-services.yaml
        // All services from included files are merged into a single services dictionary

        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var contextsDir = Path.Combine(tempDir, "Contexts");
        Directory.CreateDirectory(contextsDir);

        try
        {
            // Create main manifest with services.include (no productVersion - it's a fragment!)
            var mainManifest = @"
metadata:
  name: Business Services
  description: Business Services - all bounded context services

variables:
  REDIS_CONNECTION:
    label: Redis Connection
    type: String
    default: cachedata:6379

services:
  include:
    - Contexts/projectmanagement.yaml
    - Contexts/memo.yaml
";
            var mainFilePath = Path.Combine(tempDir, "business-services.yaml");
            await File.WriteAllTextAsync(mainFilePath, mainManifest);

            // Create include files
            var projectManagementFragment = @"
metadata:
  name: ProjectManagement

services:
  project-api:
    image: test/project-api:latest
  project-web:
    image: test/project-web:latest
";
            await File.WriteAllTextAsync(Path.Combine(contextsDir, "projectmanagement.yaml"), projectManagementFragment);

            var memoFragment = @"
metadata:
  name: Memo

services:
  memo-api:
    image: test/memo-api:latest
";
            await File.WriteAllTextAsync(Path.Combine(contextsDir, "memo.yaml"), memoFragment);

            // Act
            var result = await _parser.ParseFromFileAsync(mainFilePath);

            // Assert - All services from includes should be merged
            result.Should().NotBeNull();
            result.IsSingleStack.Should().BeTrue("Should be a single-stack manifest");
            result.IsProduct.Should().BeFalse("Should be a fragment, not a product");
            result.Services.Should().NotBeNull("Services should be merged from includes");
            result.Services.Should().HaveCount(3, "Should have 3 services total");
            result.Services.Should().ContainKey("project-api");
            result.Services.Should().ContainKey("project-web");
            result.Services.Should().ContainKey("memo-api");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ParseFromFileAsync_WithServiceIncludesAndDirectServices_MergesBoth()
    {
        // Test that services.include can be combined with direct service definitions

        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var contextsDir = Path.Combine(tempDir, "Contexts");
        Directory.CreateDirectory(contextsDir);

        try
        {
            // Create main manifest with both include and direct services
            var mainManifest = @"
metadata:
  name: Mixed Services

services:
  include:
    - Contexts/included.yaml
  direct-service:
    image: test/direct:latest
";
            var mainFilePath = Path.Combine(tempDir, "mixed.yaml");
            await File.WriteAllTextAsync(mainFilePath, mainManifest);

            // Create include file
            var includedFragment = @"
services:
  included-service:
    image: test/included:latest
";
            await File.WriteAllTextAsync(Path.Combine(contextsDir, "included.yaml"), includedFragment);

            // Act
            var result = await _parser.ParseFromFileAsync(mainFilePath);

            // Assert
            result.Services.Should().HaveCount(2);
            result.Services.Should().ContainKey("direct-service");
            result.Services.Should().ContainKey("included-service");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    #endregion

    #region Maintenance Observer Tests

    [Fact]
    public async Task ParseAsync_WithMaintenanceObserver_ParsesNestedStructure()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: AMS Business
  productVersion: '3.0.1'
services:
  app:
    image: myapp:latest
maintenance:
  observer:
    type: sqlExtendedProperty
    connectionString: ${AMS_DB}
    propertyName: ams.maintenance
    pollingInterval: 30s
    maintenanceValue: '1'
    normalValue: '0'
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Maintenance.Should().NotBeNull();
        result.Maintenance!.Observer.Should().NotBeNull();
        result.Maintenance.Observer!.Type.Should().Be("sqlExtendedProperty");
        result.Maintenance.Observer.ConnectionString.Should().Be("${AMS_DB}");
        result.Maintenance.Observer.PropertyName.Should().Be("ams.maintenance");
        result.Maintenance.Observer.PollingInterval.Should().Be("30s");
        result.Maintenance.Observer.MaintenanceValue.Should().Be("1");
        result.Maintenance.Observer.NormalValue.Should().Be("0");
    }

    [Fact]
    public async Task ParseAsync_WithHttpObserver_ParsesAllProperties()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: Web App
  productVersion: '1.0.0'
services:
  app:
    image: myapp:latest
maintenance:
  observer:
    type: http
    url: https://api.example.com/maintenance
    method: GET
    timeout: 10s
    jsonPath: $.status
    maintenanceValue: maintenance
    headers:
      Authorization: Bearer ${API_TOKEN}
      Accept: application/json
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Maintenance.Should().NotBeNull();
        result.Maintenance!.Observer.Should().NotBeNull();
        result.Maintenance.Observer!.Type.Should().Be("http");
        result.Maintenance.Observer.Url.Should().Be("https://api.example.com/maintenance");
        result.Maintenance.Observer.Method.Should().Be("GET");
        result.Maintenance.Observer.Timeout.Should().Be("10s");
        result.Maintenance.Observer.JsonPath.Should().Be("$.status");
        result.Maintenance.Observer.MaintenanceValue.Should().Be("maintenance");
        result.Maintenance.Observer.Headers.Should().ContainKey("Authorization");
        result.Maintenance.Observer.Headers.Should().ContainKey("Accept");
    }

    [Fact]
    public async Task ParseAsync_WithFileObserver_ParsesAllProperties()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: File Based
  productVersion: '1.0.0'
services:
  app:
    image: myapp:latest
maintenance:
  observer:
    type: file
    path: /app/maintenance.flag
    mode: exists
    maintenanceValue: 'true'
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Maintenance.Should().NotBeNull();
        result.Maintenance!.Observer.Should().NotBeNull();
        result.Maintenance.Observer!.Type.Should().Be("file");
        result.Maintenance.Observer.Path.Should().Be("/app/maintenance.flag");
        result.Maintenance.Observer.Mode.Should().Be("exists");
        result.Maintenance.Observer.MaintenanceValue.Should().Be("true");
    }

    [Fact]
    public async Task ParseAsync_WithSqlQueryObserver_ParsesAllProperties()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: SQL Query Based
  productVersion: '1.0.0'
services:
  app:
    image: myapp:latest
maintenance:
  observer:
    type: sqlQuery
    connectionName: DB_CONNECTION
    query: SELECT MaintenanceMode FROM dbo.SystemConfig WHERE Id = 1
    pollingInterval: 1m
    maintenanceValue: 'true'
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Maintenance.Should().NotBeNull();
        result.Maintenance!.Observer.Should().NotBeNull();
        result.Maintenance.Observer!.Type.Should().Be("sqlQuery");
        result.Maintenance.Observer.ConnectionName.Should().Be("DB_CONNECTION");
        result.Maintenance.Observer.Query.Should().Be("SELECT MaintenanceMode FROM dbo.SystemConfig WHERE Id = 1");
        result.Maintenance.Observer.PollingInterval.Should().Be("1m");
    }

    [Fact]
    public async Task ParseAsync_WithoutMaintenanceObserver_MaintenanceIsNull()
    {
        // Arrange
        var yaml = @"
version: '1.0'
metadata:
  name: Simple Stack
  productVersion: '1.0.0'
services:
  app:
    image: myapp:latest
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Maintenance.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_WithMaintenanceWithoutObserver_ObserverIsNull()
    {
        // Arrange - Future extensibility: maintenance section without observer
        var yaml = @"
version: '1.0'
metadata:
  name: Empty Maintenance
  productVersion: '1.0.0'
services:
  app:
    image: myapp:latest
maintenance: {}
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Maintenance.Should().NotBeNull();
        result.Maintenance!.Observer.Should().BeNull();
    }

    #endregion
}
