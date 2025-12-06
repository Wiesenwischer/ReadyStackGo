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
  stackVersion: '1.0.0'
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
        result.Errors.Should().Contain(e => e.Contains("No services"));
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
}
