using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Infrastructure.Manifests;

namespace ReadyStackGo.UnitTests.Manifests;

public class DockerComposeParserTests
{
    private readonly DockerComposeParser _parser;

    public DockerComposeParserTests()
    {
        var logger = new Mock<ILogger<DockerComposeParser>>();
        _parser = new DockerComposeParser(logger.Object);
    }

    [Fact]
    public async Task ParseAsync_ValidComposeFile_ReturnsDefinition()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  web:
    image: nginx:latest
    ports:
      - '8080:80'
  db:
    image: postgres:15
    environment:
      POSTGRES_PASSWORD: secret
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be("3.8");
        result.Services.Should().HaveCount(2);
        result.Services.Should().ContainKey("web");
        result.Services.Should().ContainKey("db");
        result.Services["web"].Image.Should().Be("nginx:latest");
        result.Services["web"].Ports.Should().Contain("8080:80");
        result.Services["db"].Image.Should().Be("postgres:15");
        result.Services["db"].Environment.Should().ContainKey("POSTGRES_PASSWORD");
    }

    [Fact]
    public async Task ParseAsync_WithDependsOn_ParsesDependencies()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    image: myapp:latest
    depends_on:
      - db
      - redis
  db:
    image: postgres:15
  redis:
    image: redis:7
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Services["app"].DependsOn.Should().HaveCount(2);
        result.Services["app"].DependsOn.Should().Contain("db");
        result.Services["app"].DependsOn.Should().Contain("redis");
    }

    [Fact]
    public async Task ParseAsync_WithVolumes_ParsesVolumes()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  db:
    image: postgres:15
    volumes:
      - db-data:/var/lib/postgresql/data
      - ./config:/etc/postgresql/conf.d
volumes:
  db-data:
    driver: local
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Services["db"].Volumes.Should().HaveCount(2);
        result.Volumes.Should().ContainKey("db-data");
        result.Volumes!["db-data"].Driver.Should().Be("local");
    }

    [Fact]
    public async Task DetectVariablesAsync_SimpleVariables_DetectsAll()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    image: myapp:${VERSION}
    environment:
      DATABASE_URL: ${DATABASE_URL}
      API_KEY: ${API_KEY}
";

        // Act
        var variables = await _parser.DetectVariablesAsync(yaml);

        // Assert
        variables.Should().HaveCount(3);
        variables.Should().Contain(v => v.Name == "VERSION");
        variables.Should().Contain(v => v.Name == "DATABASE_URL");
        variables.Should().Contain(v => v.Name == "API_KEY");
        variables.Where(v => v.IsRequired).Should().HaveCount(3);
    }

    [Fact]
    public async Task DetectVariablesAsync_VariablesWithDefaults_ParsesDefaults()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    image: myapp:${VERSION:-latest}
    environment:
      PORT: ${PORT:-8080}
      DEBUG: ${DEBUG:-false}
";

        // Act
        var variables = await _parser.DetectVariablesAsync(yaml);

        // Assert
        variables.Should().HaveCount(3);

        var version = variables.First(v => v.Name == "VERSION");
        version.DefaultValue.Should().Be("latest");
        version.IsRequired.Should().BeFalse();

        var port = variables.First(v => v.Name == "PORT");
        port.DefaultValue.Should().Be("8080");

        var debug = variables.First(v => v.Name == "DEBUG");
        debug.DefaultValue.Should().Be("false");
    }

    [Fact]
    public async Task DetectVariablesAsync_MixedRequiredAndOptional_ClassifiesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    image: myapp:${VERSION:-latest}
    environment:
      DATABASE_URL: ${DATABASE_URL}
      PORT: ${PORT:-3000}
";

        // Act
        var variables = await _parser.DetectVariablesAsync(yaml);

        // Assert
        var required = variables.Where(v => v.IsRequired).ToList();
        var optional = variables.Where(v => !v.IsRequired).ToList();

        required.Should().HaveCount(1);
        required.Should().Contain(v => v.Name == "DATABASE_URL");

        optional.Should().HaveCount(2);
        optional.Should().Contain(v => v.Name == "VERSION");
        optional.Should().Contain(v => v.Name == "PORT");
    }

    [Fact]
    public async Task ConvertToDeploymentPlanAsync_ValidCompose_CreatesSteps()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  web:
    image: nginx:${VERSION:-latest}
    ports:
      - '8080:80'
  db:
    image: postgres:15
";
        var definition = await _parser.ParseAsync(yaml);
        var variables = new Dictionary<string, string>
        {
            ["VERSION"] = "1.21"
        };

        // Act
        var plan = await _parser.ConvertToDeploymentPlanAsync(definition, variables, "my-stack");

        // Assert
        plan.Should().NotBeNull();
        plan.StackVersion.Should().Be("my-stack");
        plan.Steps.Should().HaveCount(2);

        var webStep = plan.Steps.First(s => s.ContextName == "web");
        webStep.Image.Should().Be("nginx:1.21");
        webStep.Ports.Should().Contain("8080:80");
    }

    [Fact]
    public async Task ConvertToDeploymentPlanAsync_WithDependencies_OrdersCorrectly()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    image: myapp:latest
    depends_on:
      - db
  db:
    image: postgres:15
";
        var definition = await _parser.ParseAsync(yaml);

        // Act
        var plan = await _parser.ConvertToDeploymentPlanAsync(definition, new Dictionary<string, string>(), "stack");

        // Assert
        var dbStep = plan.Steps.First(s => s.ContextName == "db");
        var appStep = plan.Steps.First(s => s.ContextName == "app");

        dbStep.Order.Should().BeLessThan(appStep.Order);
    }

    [Fact]
    public async Task ValidateAsync_ValidCompose_ReturnsValid()
    {
        // Arrange
        var yaml = @"
version: '3.8'
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
version: '3.8'
services:
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No services"));
    }

    [Fact]
    public async Task ValidateAsync_MissingImage_ReturnsInvalid()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  web:
    ports:
      - '8080:80'
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("image") || e.Contains("build"));
    }

    [Fact]
    public async Task ValidateAsync_NonExistentDependency_ReturnsInvalid()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  web:
    image: nginx:latest
    depends_on:
      - nonexistent
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("nonexistent"));
    }

    [Fact]
    public async Task ValidateAsync_WithBuild_AddsWarning()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    build: ./app
";

        // Act
        var result = await _parser.ValidateAsync(yaml);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("build"));
    }

    [Fact]
    public async Task ParseAsync_EnvironmentAsList_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    image: myapp:latest
    environment:
      - KEY1=value1
      - KEY2=value2
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Services["app"].Environment.Should().ContainKey("KEY1");
        result.Services["app"].Environment!["KEY1"].Should().Be("value1");
        result.Services["app"].Environment.Should().ContainKey("KEY2");
        result.Services["app"].Environment!["KEY2"].Should().Be("value2");
    }

    [Fact]
    public async Task ParseAsync_WithNetworks_ParsesNetworks()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    image: myapp:latest
    networks:
      - frontend
      - backend
networks:
  frontend:
    driver: bridge
  backend:
    driver: bridge
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Services["app"].Networks.Should().HaveCount(2);
        result.Networks.Should().HaveCount(2);
        result.Networks.Should().ContainKey("frontend");
        result.Networks.Should().ContainKey("backend");
    }

    [Fact]
    public async Task ParseAsync_WithHealthCheck_ParsesHealthCheck()
    {
        // Arrange
        var yaml = @"
version: '3.8'
services:
  app:
    image: myapp:latest
    healthcheck:
      test: ['CMD', 'curl', '-f', 'http://localhost/health']
      interval: 30s
      timeout: 10s
      retries: 3
";

        // Act
        var result = await _parser.ParseAsync(yaml);

        // Assert
        result.Services["app"].HealthCheck.Should().NotBeNull();
        result.Services["app"].HealthCheck!.Test.Should().HaveCount(4);
        result.Services["app"].HealthCheck!.Interval.Should().Be("30s");
        result.Services["app"].HealthCheck!.Timeout.Should().Be("10s");
        result.Services["app"].HealthCheck!.Retries.Should().Be(3);
    }
}
