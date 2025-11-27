using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Domain.Organizations;

namespace ReadyStackGo.UnitTests.Configuration;

public class ConfigStoreSerializationTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigStoreSerializationTests()
    {
        // Use the same options as ConfigStore
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
    }

    [Fact]
    public void SystemConfig_SerializeAndDeserialize_RoundTrips()
    {
        // Arrange
        var config = new SystemConfig
        {
            Organization = Organization.Create("test-org", "Test Organization"),
            BaseUrl = "http://localhost:5000",
            HttpPort = 5000,
            HttpsPort = 5001,
            DockerNetwork = "rsgo-net",
            Mode = DeploymentMode.SingleNode,
            WizardState = WizardState.Installed,
            InstalledVersion = "v0.4.0"
        };

        config.Organization.AddEnvironment(new DockerSocketEnvironment
        {
            Id = "local-docker",
            Name = "Local Docker",
            SocketPath = "npipe://./pipe/docker_engine",
            IsDefault = true
        });

        // Act
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Organization.Should().NotBeNull();
        deserialized.Organization!.Id.Should().Be("test-org");
        deserialized.Organization.Name.Should().Be("Test Organization");
        deserialized.Organization.Environments.Should().HaveCount(1);
        deserialized.BaseUrl.Should().Be("http://localhost:5000");
        deserialized.HttpPort.Should().Be(5000);
        deserialized.HttpsPort.Should().Be(5001);
        deserialized.DockerNetwork.Should().Be("rsgo-net");
        deserialized.Mode.Should().Be(DeploymentMode.SingleNode);
        deserialized.WizardState.Should().Be(WizardState.Installed);
        deserialized.InstalledVersion.Should().Be("v0.4.0");
    }

    [Fact]
    public void Environment_PolymorphicSerialization_PreservesType()
    {
        // Arrange
        var config = new SystemConfig
        {
            Organization = Organization.Create("test-org", "Test Organization")
        };

        var environment = new DockerSocketEnvironment
        {
            Id = "local-docker",
            Name = "Local Docker",
            SocketPath = "npipe://./pipe/docker_engine",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };
        config.Organization.AddEnvironment(environment);

        // Act
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Organization!.Environments.Should().HaveCount(1);

        var env = deserialized.Organization.Environments[0];
        env.Should().BeOfType<DockerSocketEnvironment>();
        env.Id.Should().Be("local-docker");
        env.Name.Should().Be("Local Docker");
        env.IsDefault.Should().BeTrue();

        var dockerEnv = (DockerSocketEnvironment)env;
        dockerEnv.SocketPath.Should().Be("npipe://./pipe/docker_engine");
    }

    [Fact]
    public void Environment_GetEnvironmentById_ReturnsCorrectEnvironment()
    {
        // Arrange
        var config = new SystemConfig
        {
            Organization = Organization.Create("test-org", "Test Organization")
        };

        config.Organization.AddEnvironment(new DockerSocketEnvironment
        {
            Id = "env-1",
            Name = "Environment 1",
            SocketPath = "unix:///var/run/docker.sock",
            IsDefault = true
        });

        config.Organization.AddEnvironment(new DockerSocketEnvironment
        {
            Id = "env-2",
            Name = "Environment 2",
            SocketPath = "npipe://./pipe/docker_engine"
        });

        // Act
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);
        var environment = deserialized!.Organization!.GetEnvironment("env-2");

        // Assert
        environment.Should().NotBeNull();
        environment!.Id.Should().Be("env-2");
        environment.Name.Should().Be("Environment 2");
    }

    [Fact]
    public void Enums_SerializeAsStrings()
    {
        // Arrange
        var config = new SystemConfig
        {
            Mode = DeploymentMode.SingleNode,
            WizardState = WizardState.Installed
        };

        // Act
        var json = JsonSerializer.Serialize(config, _jsonOptions);

        // Assert
        json.Should().Contain("\"mode\": \"SingleNode\"");
        json.Should().Contain("\"wizardState\": \"Installed\"");
    }

    [Fact]
    public void Enums_DeserializeFromIntegers_BackwardsCompatibility()
    {
        // Arrange - This represents old config files with integer enum values
        var json = @"{
            ""baseUrl"": ""http://localhost:5000"",
            ""httpPort"": 5000,
            ""httpsPort"": 5001,
            ""dockerNetwork"": ""rsgo-net"",
            ""mode"": 0,
            ""wizardState"": 3,
            ""installedVersion"": ""v0.4.0""
        }";

        // Act
        var deserialized = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Mode.Should().Be(DeploymentMode.SingleNode);
        deserialized.WizardState.Should().Be(WizardState.Installed);
    }

    [Fact]
    public void Environment_PolymorphicDeserialization_FromJson()
    {
        // Arrange - JSON format that matches what ConfigStore produces
        var json = @"{
            ""organization"": {
                ""id"": ""test-org"",
                ""name"": ""Test Organization"",
                ""environments"": [
                    {
                        ""$type"": ""docker-socket"",
                        ""socketPath"": ""npipe://./pipe/docker_engine"",
                        ""id"": ""local-docker"",
                        ""name"": ""Local Docker"",
                        ""isDefault"": true,
                        ""createdAt"": ""2025-01-01T00:00:00Z""
                    }
                ],
                ""createdAt"": ""2025-01-01T00:00:00Z"",
                ""updatedAt"": ""2025-01-01T00:00:00Z""
            },
            ""baseUrl"": ""http://localhost:5000"",
            ""httpPort"": 5000,
            ""httpsPort"": 5001,
            ""dockerNetwork"": ""rsgo-net"",
            ""mode"": ""SingleNode"",
            ""wizardState"": ""Installed"",
            ""installedVersion"": ""v0.4.0""
        }";

        // Act
        var deserialized = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Organization.Should().NotBeNull();
        deserialized.Organization!.Environments.Should().HaveCount(1);

        var env = deserialized.Organization.Environments[0];
        env.Should().BeOfType<DockerSocketEnvironment>();

        var dockerEnv = (DockerSocketEnvironment)env;
        dockerEnv.Id.Should().Be("local-docker");
        dockerEnv.Name.Should().Be("Local Docker");
        dockerEnv.SocketPath.Should().Be("npipe://./pipe/docker_engine");
        dockerEnv.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Environment_PolymorphicDeserialization_FromJson_WithIntegerEnums()
    {
        // Arrange - JSON format with integer enum values (backwards compatibility)
        var json = @"{
            ""organization"": {
                ""id"": ""test-org"",
                ""name"": ""Test Organization"",
                ""environments"": [
                    {
                        ""$type"": ""docker-socket"",
                        ""socketPath"": ""npipe://./pipe/docker_engine"",
                        ""id"": ""local-docker"",
                        ""name"": ""Local Docker"",
                        ""isDefault"": true,
                        ""createdAt"": ""2025-01-01T00:00:00Z""
                    }
                ],
                ""createdAt"": ""2025-01-01T00:00:00Z"",
                ""updatedAt"": ""2025-01-01T00:00:00Z""
            },
            ""baseUrl"": ""http://localhost:5000"",
            ""httpPort"": 5000,
            ""httpsPort"": 5001,
            ""dockerNetwork"": ""rsgo-net"",
            ""mode"": 0,
            ""wizardState"": 3,
            ""installedVersion"": ""v0.4.0""
        }";

        // Act
        var deserialized = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Organization.Should().NotBeNull();
        deserialized.Organization!.Environments.Should().HaveCount(1);

        var env = deserialized.Organization.Environments[0];
        env.Should().BeOfType<DockerSocketEnvironment>();

        deserialized.Mode.Should().Be(DeploymentMode.SingleNode);
        deserialized.WizardState.Should().Be(WizardState.Installed);
    }

    [Fact]
    public void SecurityConfig_SerializeAndDeserialize_RoundTrips()
    {
        // Arrange
        var config = new SecurityConfig
        {
            LocalAdmin = new AdminUser
            {
                Username = "admin",
                PasswordHash = "$2a$11$...",
                Salt = "$2a$11$...",
                Role = "admin"
            },
            LocalAdminFallbackEnabled = true
        };

        // Act
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SecurityConfig>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.LocalAdmin.Should().NotBeNull();
        deserialized.LocalAdmin!.Username.Should().Be("admin");
        deserialized.LocalAdmin.PasswordHash.Should().Be("$2a$11$...");
        deserialized.LocalAdmin.Salt.Should().Be("$2a$11$...");
        deserialized.LocalAdmin.Role.Should().Be("admin");
        deserialized.LocalAdminFallbackEnabled.Should().BeTrue();
    }

    [Fact]
    public void ActualConfigFile_DeserializesCorrectly()
    {
        // Arrange - This is the actual format from rsgo.system.json
        var json = @"{
            ""organization"": {
                ""id"": ""ams"",
                ""name"": ""ams.Solution AG"",
                ""environments"": [
                    {
                        ""$type"": ""docker-socket"",
                        ""socketPath"": ""npipe://./pipe/docker_engine"",
                        ""id"": ""local-docker"",
                        ""name"": ""Local Docker"",
                        ""isDefault"": true,
                        ""createdAt"": ""2025-11-21T07:41:53.2359294Z""
                    }
                ],
                ""createdAt"": ""2025-11-21T07:41:27.8228657Z"",
                ""updatedAt"": ""2025-11-21T07:41:53.2366897Z""
            },
            ""baseUrl"": ""http://localhost:5000"",
            ""httpPort"": 5000,
            ""httpsPort"": 5001,
            ""dockerNetwork"": ""rsgo-net"",
            ""mode"": 0,
            ""wizardState"": 3,
            ""installedVersion"": ""v0.4.0""
        }";

        // Act
        var deserialized = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Organization.Should().NotBeNull();
        deserialized.Organization!.Id.Should().Be("ams");
        deserialized.Organization.Environments.Should().HaveCount(1);

        var env = deserialized.Organization.GetEnvironment("local-docker");
        env.Should().NotBeNull("GetEnvironment should find the environment");
        env!.Id.Should().Be("local-docker");
        env.Should().BeOfType<DockerSocketEnvironment>();

        var dockerEnv = (DockerSocketEnvironment)env;
        dockerEnv.SocketPath.Should().Be("npipe://./pipe/docker_engine");
    }

    [Fact]
    public void MultipleEnvironments_SerializeAndDeserialize_PreservesAll()
    {
        // Arrange
        var config = new SystemConfig
        {
            Organization = Organization.Create("test-org", "Test Organization"),
            WizardState = WizardState.Installed
        };

        config.Organization.AddEnvironment(new DockerSocketEnvironment
        {
            Id = "production",
            Name = "Production",
            SocketPath = "npipe://./pipe/docker_engine",
            IsDefault = true
        });

        config.Organization.AddEnvironment(new DockerSocketEnvironment
        {
            Id = "staging",
            Name = "Staging",
            SocketPath = "tcp://staging-server:2375"
        });

        config.Organization.AddEnvironment(new DockerSocketEnvironment
        {
            Id = "development",
            Name = "Development",
            SocketPath = "unix:///var/run/docker.sock"
        });

        // Act
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Organization!.Environments.Should().HaveCount(3);

        var prod = deserialized.Organization.GetEnvironment("production");
        prod.Should().NotBeNull();
        prod!.IsDefault.Should().BeTrue();
        ((DockerSocketEnvironment)prod).SocketPath.Should().Be("npipe://./pipe/docker_engine");

        var staging = deserialized.Organization.GetEnvironment("staging");
        staging.Should().NotBeNull();
        ((DockerSocketEnvironment)staging!).SocketPath.Should().Be("tcp://staging-server:2375");

        var dev = deserialized.Organization.GetEnvironment("development");
        dev.Should().NotBeNull();
        ((DockerSocketEnvironment)dev!).SocketPath.Should().Be("unix:///var/run/docker.sock");
    }

    [Fact]
    public void RealConfigFile_DeserializesFromDisk()
    {
        // Arrange - Read the actual config file from disk
        var configPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ReadyStackGo.Api", "config", "rsgo.system.json"));

        if (!File.Exists(configPath))
        {
            // Skip if config file doesn't exist (CI environment)
            return;
        }

        var json = File.ReadAllText(configPath);

        // Act
        var systemConfig = JsonSerializer.Deserialize<SystemConfig>(json, _jsonOptions);

        // Assert - Verify the config was loaded and environments are accessible
        systemConfig.Should().NotBeNull();
        systemConfig!.Organization.Should().NotBeNull("Organization should be deserialized");
        systemConfig.Organization!.Environments.Should().NotBeEmpty("Environments should be deserialized");
        systemConfig.Organization.Environments.Should().HaveCount(1, "Should have exactly one environment");

        // Check that we can find the local environment (actual config uses "local" not "local-docker")
        var local = systemConfig.Organization.GetEnvironment("local");
        local.Should().NotBeNull("GetEnvironment('local') should find the environment");
        local!.Id.Should().Be("local");
        local.Should().BeOfType<DockerSocketEnvironment>("Environment should be deserialized as DockerSocketEnvironment");

        var dockerEnv = (DockerSocketEnvironment)local;
        dockerEnv.SocketPath.Should().Be("npipe://./pipe/docker_engine");
        dockerEnv.IsDefault.Should().BeTrue();
    }
}
