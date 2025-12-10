using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Infrastructure.Manifests;

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
        result.Variables["EMAIL"].Pattern.Should().NotBeNullOrEmpty();
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
